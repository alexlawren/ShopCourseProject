# Orders & Checkout API Specification

**Service**: `Shop.OrderService`  
**Base Paths**: `/api/orders`, `/api/admin/orders`  
**Authentication**: JWT Bearer (`Authorization: Bearer <token>`) — Required for all endpoints.

---

## Overview

The Orders API provides checkout processing, simulated payment, order lifecycle transitions, customer cancellations, and administrative order fulfillment. Checkout and cancellation coordinate with the catalog inventory via internal gRPC (`contracts/grpc/catalog_stock.proto`).

---

## State Machines

### Order Lifecycle

```
Created
  ├──→ Confirmed ──→ Processing ──→ Shipped ──→ Completed (Terminal)
  │        │             │
  └───┬────┴─────────────┘
      ▼
  Cancelled (Terminal)
```

- **Allowed Forward Transitions**:
  - `Created → Confirmed` (via simulated payment or manual Admin confirmation)
  - `Confirmed → Processing` (Admin fulfillment)
  - `Processing → Shipped` (Admin fulfillment)
  - `Shipped → Completed` (Admin fulfillment)
- **Allowed Cancellation Transitions**:
  - `Created → Cancelled`
  - `Confirmed → Cancelled`
  - `Processing → Cancelled`
- **Forbidden Transitions**:
  - `Shipped → Cancelled` (409 Conflict: order already shipped)
  - `Completed → Cancelled` (409 Conflict: order completed)
  - `Cancelled → any` (terminal state)
  - Any backwards or skip transition (e.g. `Created → Processing`, `Confirmed → Completed`, `Completed → Processing`).

### Payment Lifecycle

```
Pending ──→ Paid
   │          │
   └──┬───────┘ (Cancellation)
      ▼
  Cancelled
```

- `Pending → Paid`: Triggered via `POST /api/orders/{id}/pay`.
- `Pending → Cancelled`: When unpaid `Created` order is cancelled.
- `Paid → Cancelled`: When paid `Confirmed` or `Processing` order is cancelled.  
  *(For this course project, cancelling a paid order represents a simulated refund/void; no external payment gateway is integrated).*

---

## Customer Endpoints

### 1. Checkout (Create Order)
- **Method**: `POST`
- **Path**: `/api/orders`
- **Headers**: `Authorization: Bearer <token>`
- **Body**: `{ "requestId": "<guid>" }`
- **Response**: `201 Created` with `OrderDetailsDto` (or `200 OK` on idempotent retry).

### 2. List Customer Orders
- **Method**: `GET`
- **Path**: `/api/orders?page=1&pageSize=20`
- **Headers**: `Authorization: Bearer <token>`
- **Response**: `200 OK` with `PagedResult<OrderListItemDto>`.

### 3. Get Order By Id
- **Method**: `GET`
- **Path**: `/api/orders/{id}`
- **Headers**: `Authorization: Bearer <token>`
- **Response**: `200 OK` with `OrderDetailsDto` (or `404 Not Found` if non-existent or owned by another user).

### 4. Pay Order (Simulated Payment)
Executes simulated payment for an order owned by the caller:
- If `CancellationState == Pending`: `409 Conflict` (`ORDER_CANCELLATION_IN_PROGRESS`). Payment cannot occur while cancellation is in progress.
- If `PaymentStatus == Paid`: Idempotent `200 OK` (no state or history change).
- If `PaymentStatus == Cancelled` or `OrderStatus == Cancelled`: `409 Conflict` (`PAYMENT_CANCELLED`).
- Transitions `PaymentStatus` from `Pending` to `Paid`.
- If `OrderStatus == Created`: transitions to `Confirmed` and logs history entry.
- **Method**: `POST`
- **Path**: `/api/orders/{id}/pay`
- **Headers**: `Authorization: Bearer <token>`
- **Response — 200 OK**:
```json
{
  "id": "874d3220-9b6b-4da8-97b2-a82a42082523",
  "status": "Confirmed",
  "paymentStatus": "Paid",
  "totalAmount": 226.00,
  "createdAtUtc": "2026-10-02T19:47:12.123456Z",
  "updatedAtUtc": "2026-10-02T19:48:05.654321Z",
  "items": [...],
  "statusHistory": [
    { "status": "Created", "changedAtUtc": "2026-10-02T19:47:12.123456Z" },
    { "status": "Confirmed", "changedAtUtc": "2026-10-02T19:48:05.654321Z" }
  ]
}
```
- **Error Responses**:
  - `401 Unauthorized`
  - `404 Not Found` (if not found or wrong owner)
  - `409 Conflict` (`ORDER_CANCELLATION_IN_PROGRESS`, `PAYMENT_CANCELLED`)

### 5. Cancel Customer Order
Cancels an order owned by the caller using a durable 3-phase orchestration algorithm:
- Allowed only when `OrderStatus` is `Created`, `Confirmed`, or `Processing`.
- If already `Cancelled`: Idempotent `200 OK` (returns current order, no stock change).
- If `Shipped` or `Completed`: `409 Conflict` (`ORDER_CANNOT_BE_CANCELLED`).
- **Phase A (Local Durable Intent)**: Under PostgreSQL row lock (`FOR UPDATE`), verifies state and persists `CancellationState = Pending` to `shop_orders`.
- **Phase B (Catalog Stock Return)**: Calls Catalog gRPC `CancelCommittedReservation(ReservationId)` to safely restore reserved stock. If Catalog returns `503 Unavailable`, order remains `Pending` and client retries.
- **Phase C (Local Finalization)**: Under a new row lock, updates `OrderStatus = Cancelled`, `PaymentStatus = Cancelled`, `CancellationState = None`, and logs a single history entry.
- **Retry Recovery**: If Phase C fails or is interrupted, the order remains durable `Pending`. A repeated cancel request automatically resumes Phase B/C (idempotent gRPC call) and finalizes to `Cancelled`.
- **Method**: `POST`
- **Path**: `/api/orders/{id}/cancel`
- **Headers**: `Authorization: Bearer <token>`
- **Response**: `200 OK` with updated `OrderDetailsDto`.
- **Error Responses**:
  - `401 Unauthorized`
  - `404 Not Found`
  - `409 Conflict` (`ORDER_CANNOT_BE_CANCELLED`)
  - `502 Bad Gateway` (`DOWNSTREAM_ERROR`)
  - `503 Service Unavailable` (`CATALOG_UNAVAILABLE`)

---

## Admin Endpoints

All admin endpoints require `[Authorize(Roles = "Admin")]`. Anonymous requests receive `401 Unauthorized`; customers receive `403 Forbidden`.

### 1. List All Orders
Returns a paginated list of all customer orders, sorted by `createdAtUtc` descending.
- **Method**: `GET`
- **Path**: `/api/admin/orders?page=1&pageSize=20&status=Confirmed&paymentStatus=Paid`
- **Headers**: `Authorization: Bearer <admin-token>`
- **Query Parameters**:
  - `page` (`int`, default `1`, `>= 1`)
  - `pageSize` (`int`, default `20`, `1..100`)
  - `status` (`string`, optional filter: `Created`, `Confirmed`, `Processing`, `Shipped`, `Completed`, `Cancelled`)
  - `paymentStatus` (`string`, optional filter: `Pending`, `Paid`, `Cancelled`)
- **Response — 200 OK**:
```json
{
  "items": [
    {
      "id": "874d3220-9b6b-4da8-97b2-a82a42082523",
      "userId": "01a0fe26-4f1a-7946-a084-d03b518fe722",
      "status": "Confirmed",
      "paymentStatus": "Paid",
      "totalAmount": 226.00,
      "createdAtUtc": "2026-10-02T19:47:12.123456Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

### 2. Get Order Details (Admin)
Returns complete details including `userId`, item snapshots, and history.
- **Method**: `GET`
- **Path**: `/api/admin/orders/{id}`
- **Headers**: `Authorization: Bearer <admin-token>`
- **Response**: `200 OK` with `AdminOrderDetailsDto` (or `404 Not Found`).

### 3. Update Order Status (Fulfillment Lifecycle)
Advances order through the fulfillment pipeline (`Confirmed → Processing`, `Processing → Shipped`, `Shipped → Completed`).
- **Forbidden**: Passing `"status": "Cancelled"` via this generic status patch returns `409 Conflict` (cancellation must use the dedicated cancellation endpoint to guarantee stock restoration).
- **Idempotent**: Re-applying the current status returns `200 OK` without duplicating history.
- **Method**: `PATCH`
- **Path**: `/api/admin/orders/{id}/status`
- **Headers**: `Authorization: Bearer <admin-token>`
- **Request Body**:
```json
{
  "status": "Processing"
}
```
- **Response**: `200 OK` with updated `AdminOrderDetailsDto`.
- **Error Responses**:
  - `400 Bad Request` (`INVALID_STATUS`)
  - `404 Not Found`
  - `409 Conflict` (`ORDER_CANCELLATION_IN_PROGRESS`, `INVALID_ORDER_TRANSITION`)

### 4. Admin Cancel Order
Allows administrators to cancel orders in `Created`, `Confirmed`, or `Processing` status without owner restriction. Uses the exact same 3-phase concurrency-safe stock return logic as customer cancellation.
- **Method**: `POST`
- **Path**: `/api/admin/orders/{id}/cancel`
- **Headers**: `Authorization: Bearer <admin-token>`
- **Response**: `200 OK` with `AdminOrderDetailsDto`.
- **Error Responses**:
  - `404 Not Found`
  - `409 Conflict` (`ORDER_CANNOT_BE_CANCELLED`)
  - `502 Bad Gateway` (`DOWNSTREAM_ERROR`)
  - `503 Service Unavailable` (`CATALOG_UNAVAILABLE`)

---

## Concurrency and Race Coordination

### Cancel vs Admin Ship Race & Distributed Consistency
When an order in `Processing` status is concurrently cancelled and marked as `Shipped`:

1. **Durable Local Intent (Phase A)**:
   - Cancel acquires a PostgreSQL row lock (`SELECT 1 FROM "Orders" WHERE "Id" = @id FOR UPDATE`) and sets `CancellationState = Pending`, committing immediately.
2. **Catalog Stock Restoration (Phase B)**:
   - Cancel invokes Catalog gRPC `CancelCommittedReservation`. Catalog atomically sets reservation to `Cancelled` and refunds `StockQuantity`.
3. **Local Finalization (Phase C)**:
   - Cancel acquires a second row lock, updates `Status = Cancelled`, `PaymentStatus = Cancelled`, `CancellationState = None`, and logs one history entry.

#### Distributed Race Closed:
- **Admin Status Transition Protection**: If an Admin attempts to change order status (e.g. `Processing → Shipped`) while `CancellationState == Pending`, the request is immediately rejected with `409 Conflict` (`ORDER_CANCELLATION_IN_PROGRESS`).
- **Simulated Payment Protection**: If a customer attempts to pay an order while `CancellationState == Pending`, the request is rejected with `409 Conflict` (`ORDER_CANCELLATION_IN_PROGRESS`).
- **Post-Catalog-Success Local Finalization Failure**: If Phase B succeeds (stock is restored in Catalog) but OrderService crashes or fails its local transaction during Phase C, the order remains safely locked in `CancellationState = Pending`. Admin `Shipped` is blocked (409 Conflict). A subsequent retry of `CancelOrder` resumes orchestration, calls Catalog's idempotent gRPC endpoint, and successfully finalizes the local order to `Cancelled` without duplicate stock refunds or duplicate history records.
- **Invariant**: It is impossible for an order to be marked `Shipped` while stock is refunded, or marked `Cancelled` without stock being refunded.
