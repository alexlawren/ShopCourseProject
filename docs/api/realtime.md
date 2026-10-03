# Real-Time Notifications API (SignalR)

Real-time notification hubs allow clients to receive immediate server-push notifications about order processing and catalog changes via ASP.NET Core SignalR.

## Architectural Principles

1. **Server Push**: SignalR is used strictly for asynchronous unidirectional push notifications from backend services to connected clients.
2. **Transport Hierarchy**: WebSockets (`HttpTransportType.WebSockets`) is preferred when supported by the client environment, falling back to Server-Sent Events or Long Polling.
3. **Best-Effort Delivery**: Notifications are inherently best-effort. If delivery fails (client disconnect, hub timeout, network error), committed database transactions and business operations are **never** rolled back.
4. **Source of Truth**: PostgreSQL and the REST APIs remain the authoritative source of truth. Upon reconnection, clients should re-synchronize state via standard REST endpoints (`GET /api/orders`, `GET /api/catalog/products`).
5. **No Client Mutations**: SignalR hubs are receive-only for clients. All state mutations must be initiated through standard REST APIs.

---

## 1. OrderHub (`Shop.OrderService`)

### Endpoint
- **URL**: `/hubs/orders`
- **Protocol**: SignalR over WebSocket / SSE / Long Polling
- **Authentication**: **Required** (`[Authorize]`). Anonymous connections are rejected with HTTP 401 Unauthorized.

### Authentication & JWT over WebSocket
Web browsers cannot supply custom HTTP headers during initial WebSocket handshake. Therefore, `OrderService` accepts the JWT access token via the `access_token` query string parameter during negotiation/connection.

> **Security Rule**: The `access_token` query parameter is permitted **strictly and exclusively** when the request path starts with `/hubs/orders`. All REST API endpoints (`/api/...`) reject query string tokens and require the standard `Authorization: Bearer <token>` header.

### Group Model & User Isolation
Upon connection (`OnConnectedAsync`):
1. **User Group**: The server extracts the authenticated `UserId` (parsed from the JWT `sub` claim as a `Guid`). The connection automatically joins the group:
   ```text
   user:{userId}
   ```
   *Example*: `user:123e4567-e89b-12d3-a456-426614174000`
   > **Security Rule**: Clients cannot join or specify arbitrary user groups. There are no client-invokable `JoinUserGroup` or subscription methods. User isolation is enforced server-side.
2. **Admin Group**: If the authenticated `ClaimsPrincipal` contains the `Admin` role claim, the connection additionally joins:
   ```text
   admins
   ```

### Events & Payloads

#### `OrderCreated`
Emitted after successful checkout and durable order persistence.
- **Recipients**: `user:{order.UserId}` and `admins`
- **Payload**:
  ```json
  {
    "orderId": "123e4567-e89b-12d3-a456-426614174000",
    "status": "Created",
    "paymentStatus": "Pending",
    "totalAmount": 199.98,
    "createdAtUtc": "2026-10-03T12:00:00Z"
  }
  ```
- **Guarantees**:
  - Emitted only after local database transaction commit and successful Catalog stock reservation confirmation.
  - Idempotent repeated checkout requests with the same `RequestId` do **not** re-emit `OrderCreated`.

#### `OrderStatusChanged`
Emitted whenever an order's lifecycle status transitions.
- **Recipients**: `user:{order.UserId}` and `admins`
- **Payload**:
  ```json
  {
    "orderId": "123e4567-e89b-12d3-a456-426614174000",
    "status": "Confirmed",
    "changedAtUtc": "2026-10-03T12:05:00Z"
  }
  ```
- **Trigger Scenarios**:
  - Payment completed: transitions `Created → Confirmed`.
  - Admin advancement: `Confirmed → Processing`, `Processing → Shipped`, `Shipped → Completed`.
  - Cancellation: transitions to `Cancelled` after Phase C local transaction commit.
- **Guarantees**:
  - Emitted strictly after database commit.
  - Idempotent operations resulting in no state change do **not** emit duplicate events.

#### `PaymentStatusChanged`
Emitted whenever an order's payment status changes.
- **Recipients**: `user:{order.UserId}` and `admins`
- **Payload**:
  ```json
  {
    "orderId": "123e4567-e89b-12d3-a456-426614174000",
    "paymentStatus": "Paid",
    "changedAtUtc": "2026-10-03T12:05:00Z"
  }
  ```
- **Trigger Scenarios**:
  - Payment completed: `Pending → Paid`.
  - Cancellation: `Pending/Paid → Cancelled` (only if payment status actually changed).
- **Guarantees**:
  - Emitted strictly after database commit.
  - Idempotent repeated payment or cancellation requests do not re-emit duplicate events.

> **Information Protection**: Events do not contain internal orchestration identifiers such as `CheckoutRequestId`, `ReservationId`, `CancellationState`, gRPC states, or tokens.

---

## 2. CatalogHub (`Shop.CatalogService`)

### Endpoint
- **URL**: `/hubs/catalog`
- **Protocol**: SignalR over WebSocket / SSE / Long Polling
- **Authentication**: **Anonymous / Public**. Clients do not need authentication to subscribe to public catalog changes.

### Receive-Only Contract
`CatalogHub` does not expose any client-callable methods. Clients only listen to broadcast events sent by the server via `Clients.All`.

### Events & Payloads

#### `ProductChanged`
Broadcast whenever a product is created, updated, or soft-deleted by an Administrator.
- **Recipients**: All connected clients (`Clients.All`)
- **Payload**:
  ```json
  {
    "productId": "987fcdeb-51a2-43d7-9876-543210987654",
    "isActive": true,
    "updatedAtUtc": "2026-10-03T12:10:00Z"
  }
  ```
  *(For soft deletion, `isActive` is `false`)*.

#### `StockChanged`
Broadcast whenever a product's available stock quantity changes.
- **Recipients**: All connected clients (`Clients.All`)
- **Payload**:
  ```json
  {
    "productId": "987fcdeb-51a2-43d7-9876-543210987654",
    "stockQuantity": 88,
    "updatedAtUtc": "2026-10-03T12:15:00Z"
  }
  ```
- **Trigger Scenarios**:
  - Admin stock adjustment: `PATCH /api/catalog/products/{id}/stock`.
  - gRPC `ReserveStock`: inventory decremented upon successful reservation.
  - gRPC `ReleaseReservation`: inventory restored upon reservation release.
  - gRPC `CancelCommittedReservation`: inventory restored upon committed reservation cancellation.
- **Guarantees**:
  - Emitted strictly after transaction commit.
  - Multi-product reservations emit a separate `StockChanged` event for each modified product.
  - `CommitReservation` does **not** change inventory, therefore emits **0** events.
  - Rolled back or failed operations emit **0** events.
  - Idempotent repeated calls that result in no inventory changes do not emit duplicate events.
  - Does not expose internal reservation IDs, request IDs, or administrator identities.

---

## 3. Client Connection Example (.NET C#)

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

// OrderHub (Authenticated Customer / Admin)
var orderHub = new HubConnectionBuilder()
    .WithUrl("http://localhost:5054/hubs/orders", options =>
    {
        options.Transports = HttpTransportType.WebSockets;
        options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
    })
    .WithAutomaticReconnect()
    .Build();

orderHub.On<OrderCreatedEvent>("OrderCreated", ev => {
    Console.WriteLine($"Order {ev.OrderId} created with status {ev.Status}");
});

orderHub.On<OrderStatusChangedEvent>("OrderStatusChanged", ev => {
    Console.WriteLine($"Order {ev.OrderId} status changed to {ev.Status}");
});

orderHub.On<PaymentStatusChangedEvent>("PaymentStatusChanged", ev => {
    Console.WriteLine($"Order {ev.OrderId} payment status changed to {ev.PaymentStatus}");
});

await orderHub.StartAsync();

// CatalogHub (Public)
var catalogHub = new HubConnectionBuilder()
    .WithUrl("http://localhost:5058/hubs/catalog", options =>
    {
        options.Transports = HttpTransportType.WebSockets;
    })
    .WithAutomaticReconnect()
    .Build();

catalogHub.On<ProductChangedEvent>("ProductChanged", ev => {
    Console.WriteLine($"Product {ev.ProductId} changed. Active: {ev.IsActive}");
});

catalogHub.On<StockChangedEvent>("StockChanged", ev => {
    Console.WriteLine($"Stock for {ev.ProductId} updated to {ev.StockQuantity}");
});

await catalogHub.StartAsync();
```
