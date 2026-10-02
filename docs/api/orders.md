# Orders & Checkout API Specification

**Service**: `Shop.OrderService`  
**Base Path**: `/api/orders`  
**Authentication**: JWT Bearer (`Authorization: Bearer <token>`) — Required for all endpoints.

---

## Overview

The Orders API provides checkout processing and customer order history. Checkout coordinates customer carts with the catalog inventory via internal gRPC (`contracts/grpc/catalog_stock.proto`).

> **Note**: Payment simulation, order status transitions, and cancellation workflows are scheduled for Change-set №5C.2.

---

## Endpoints

### 1. Checkout (Create Order)

Atomically executes the checkout pipeline:
1. Validates `requestId` idempotency key.
2. Checks for existing order (fast-path retry).
3. Reads and snapshots current customer cart.
4. Calls Catalog `ReserveStock` via internal gRPC.
5. Verifies cart remained unmodified concurrently.
6. Persists `Order`, immutable `OrderItem` snapshots, initial `OrderStatusHistory` ("Created"), and clears cart items within a single local database transaction.
7. Calls Catalog `CommitReservation` via internal gRPC to finalize inventory reservation.

- **Method**: `POST`
- **Path**: `/api/orders`
- **Headers**:
  - `Content-Type: application/json`
  - `Authorization: Bearer <token>`

#### Request Body
```json
{
  "requestId": "e3e7ada6-6a34-4edf-ba93-c7d96ac682ab"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requestId` | `uuid` | Yes | Client-generated unique idempotency identifier. Cannot be `00000000-0000-0000-0000-000000000000`. |

#### Response — 201 Created (Initial Success)
Returned on the first successful checkout for a given `requestId`.
- **Headers**: `Location: /api/orders/{orderId}`
```json
{
  "id": "874d3220-9b6b-4da8-97b2-a82a42082523",
  "status": "Created",
  "paymentStatus": "Pending",
  "totalAmount": 226.00,
  "createdAtUtc": "2026-10-02T19:47:12.123456Z",
  "updatedAtUtc": "2026-10-02T19:47:12.123456Z",
  "items": [
    {
      "productId": "3717ad17-e5c7-414d-a964-b8b77268a253",
      "productName": "Product A",
      "unitPrice": 100.50,
      "quantity": 2,
      "lineTotal": 201.00
    },
    {
      "productId": "01490b5b-4e89-44bd-8531-6320a0ba8499",
      "productName": "Product B",
      "unitPrice": 25.00,
      "quantity": 1,
      "lineTotal": 25.00
    }
  ],
  "statusHistory": [
    {
      "status": "Created",
      "changedAtUtc": "2026-10-02T19:47:12.123456Z"
    }
  ]
}
```

#### Response — 200 OK (Idempotent Duplicate)
Returned when a request with an already-processed `requestId` is repeated by the same customer.
- Retries `CommitReservation` if previously unconfirmed.
- Returns the exact existing order without re-reserving stock or charging twice.

#### Error Responses
- **400 Bad Request**:
  - `INVALID_REQUEST_ID`: Empty or invalid GUID `requestId`.
  - `EMPTY_CART`: Customer cart is empty.
  - `CART_TOO_LARGE`: Cart exceeds 100 distinct items limit.
- **401 Unauthorized**: Missing, expired, or invalid JWT token.
- **404 Not Found**: Cart not found.
- **409 Conflict**:
  - `OUT_OF_STOCK`: One or more items have insufficient stock.
  - `PRODUCT_NOT_FOUND`: An item in the cart does not exist in catalog.
  - `PRODUCT_INACTIVE`: An item in the cart is inactive.
  - `CATEGORY_INACTIVE`: Category of an item is inactive.
  - `CART_CHANGED`: Cart was concurrently modified during checkout (reservation automatically released).
  - `REQUEST_ID_CONFLICT`: The `requestId` was already used by a different customer.
- **502 Bad Gateway**: Downstream gRPC protocol error or response validation failure (`DOWNSTREAM_ERROR`).
- **503 Service Unavailable**: Catalog service timed out or is unreachable (`CATALOG_UNAVAILABLE`).
  - *If returned after local order persistence*: Client must retry with the same `requestId`; fast-path will retry `CommitReservation`.

---

### 2. List Customer Orders

Returns a paginated list of orders owned by the authenticated customer, sorted by `createdAtUtc` descending.

- **Method**: `GET`
- **Path**: `/api/orders?page=1&pageSize=20`
- **Headers**:
  - `Authorization: Bearer <token>`

#### Query Parameters
| Parameter | Type | Default | Constraints | Description |
|---|---|---|---|---|
| `page` | `int` | `1` | `>= 1` | Page number. |
| `pageSize` | `int` | `20` | `1..100` | Number of items per page. |

#### Response — 200 OK
```json
{
  "items": [
    {
      "id": "874d3220-9b6b-4da8-97b2-a82a42082523",
      "status": "Created",
      "paymentStatus": "Pending",
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

---

### 3. Get Order By Id

Retrieves complete order details including immutable items snapshot and status history.

- **Method**: `GET`
- **Path**: `/api/orders/{id}`
- **Headers**:
  - `Authorization: Bearer <token>`

#### Response — 200 OK
Returns `OrderDetailsDto` matching the schema shown in `POST /api/orders`.

#### Response — 404 Not Found
Returned if the order does not exist OR if the order belongs to another customer.  
*(403 Forbidden is intentionally avoided to prevent unauthorized ID enumeration/probing).*

---

## Idempotency and Failure Compensation Rules

```
                      +-------------------+
                      |   Customer Cart   |
                      +-------------------+
                                |
                                v
               [gRPC] ReserveStock(requestId)
                                |
                +---------------+---------------+
                |                               |
          Success: true                   Success: false
                |                               |
                v                               v
    Verify Cart Unchanged               Return 409 Conflict
                |                         (no order created)
    +-----------+-----------+
    |                       |
Cart OK                Cart Changed
    |                       |
    v                       v
Local DB Save           [gRPC] ReleaseReservation
(Order + clear cart)        |
    |                       v
    |                  Return 409 CART_CHANGED
    +-----------+
    |           |
 Success     Failure
    |           |
    v           v
[gRPC] Commit  [gRPC] ReleaseReservation
    |           |
    v           v
201 Created    Return 500 / 409
```

### Critical Consistency Rules
1. **Before Durable Order Persistence**:
   - If cart changed or local DB fails: call `ReleaseReservation(reservationId)` to restore stock immediately.
2. **After Durable Order Persistence**:
   - **NEVER** call `ReleaseReservation` after local order commit.
   - If downstream `CommitReservation` fails (network glitch / timeout): return `503 Service Unavailable`.
   - On retry with the same `requestId`, the idempotent fast-path finds the existing order and retries `CommitReservation`.
