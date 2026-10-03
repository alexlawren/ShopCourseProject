# Architecture v1

## Components

| Component              | Type                 | Description                                                |
| ---------------------- | -------------------- | ---------------------------------------------------------- |
| `Shop.Web`             | Blazor WebAssembly   | Web-client for users and administrators                    |
| `Shop.Gateway`         | ASP.NET Core         | Single entry point; YARP-based API Gateway (planned)       |
| `Shop.IdentityService` | ASP.NET Core Web API | Users, roles (Customer/Admin), JWT, refresh tokens         |
| `Shop.CatalogService`  | ASP.NET Core Web API | Categories, products, search, stock reservation            |
| `Shop.OrderService`    | ASP.NET Core Web API | Cart, order processing, order statuses, payment simulation |

## Communication

Interaction patterns:

1. **External request/response**: REST + JSON (Client ↔ Services / Gateway).
2. **Internal service-to-service**: gRPC + HTTP/2 (`Shop.OrderService` ↔ `Shop.CatalogService` stock reservation).
3. **Server push**: SignalR + WebSocket (realtime notifications from services to connected clients).
   - `OrderHub`: authenticated connections; partitioned by `user:{userId}` and `admins` groups.
   - `CatalogHub`: public connections; broadcast catalog and stock updates to `Clients.All`.

```
Browser
  └─► Shop.Gateway          HTTPS / REST / JSON
        ├─► Shop.IdentityService   HTTP / REST
        ├─► Shop.CatalogService    HTTP / REST
        └─► Shop.OrderService      HTTP / REST

Shop.OrderService ──────────► Shop.CatalogService   gRPC (stock reservation)

Shop.CatalogService ─────────► Browser              SignalR / WebSocket (public catalog/stock)
Shop.OrderService   ─────────► Browser              SignalR / WebSocket (user/admin order events)
```

## Data Storage

Each service owns its own PostgreSQL database. Direct cross-service table access is forbidden.

| Service                | DbContext           | Database        |
| ---------------------- | ------------------- | --------------- |
| `Shop.IdentityService` | `IdentityDbContext` | `shop_identity` |
| `Shop.CatalogService`  | `CatalogDbContext`  | `shop_catalog`  |
| `Shop.OrderService`    | `OrderDbContext`    | `shop_orders`   |

**Rule**: A service must not query another service's database directly.

## Architecture Rules

- Each service is the sole owner of its data.
- A service must not query another service's database directly.
- Each service has its own dedicated DbContext; no shared DbContext exists.
- The central business process is order processing.
- `Shop.OrderService` uses `Shop.CatalogService` via gRPC for stock reservation.
- No direct project references between `IdentityService`, `CatalogService`, and `OrderService`.
- `Shop.Web` does not reference any backend project directly.

## Implementation Status

### IdentityService (implemented)

- ASP.NET Core Identity with `ApplicationUser : IdentityUser<Guid>`.
- PostgreSQL database `shop_identity` with EF Core migrations.
- JWT access tokens (HMAC-SHA256, configurable lifetime).
- Hashed refresh tokens with rotation.
- Roles: `Customer` (default for registration), `Admin`.
- Endpoints: register, login, refresh, logout, me.

### CatalogService (partially implemented — Change-set №4A, №4B.1, №4B.2 & №5B)

- PostgreSQL database `shop_catalog` with EF Core migrations `InitialCatalog` and `AddStockReservations`.
- Domain entities: `Category`, `Product`, `StockReservation`, `StockReservationItem`.
- Public read API: categories list, products list, product details by ID (accessible without authentication).
- Server-side features: case-insensitive search (`EF.Functions.ILike`), filtering (categoryId, minPrice, maxPrice, inStock), sorting (`priceAsc`, `priceDesc`, `nameAsc`, `nameDesc`, `newest`), and pagination (max pageSize = 100).
- Standard ASP.NET Core `ProblemDetails` error responses.
- **JWT Authorization**: CatalogService independently validates JWT tokens issued by `IdentityService` using the symmetric signing key (`Jwt:Key`), validating Issuer, Audience, Lifetime, and Signing Key without querying `shop_identity` or using `IdentityDbContext`.
- **Admin Write API**: Admin CRUD endpoints for categories and products (`POST /api/catalog/categories`, `PUT /api/catalog/categories/{id}`, `DELETE /api/catalog/categories/{id}`, `POST /api/catalog/products`, `PUT /api/catalog/products/{id}`, `DELETE /api/catalog/products/{id}`).
- **Soft Deletion**: Category and product deletion is strictly soft-delete (`IsActive = false`, `UpdatedAtUtc = UtcNow`) preserving audit history and relational integrity. Inactive categories hide their associated products from public read queries.
- **Stock Management**: Absolute stock level adjustments via `PATCH /api/catalog/products/{id}/stock` with non-negative validation and PostgreSQL check constraint enforcement.
- **Product Image Storage**:
  - `IProductImageStorage` abstraction with `LocalProductImageStorage` implementation on the local filesystem.
  - Strict validation of uploaded files (size limit 5 MiB, allowed formats JPEG/PNG/WEBP, Content-Type matching, binary magic byte verification).
  - Secure server-generated GUID filenames preventing client filename usage and path traversal attacks.
  - `Product.ImagePath` stores relative public URLs (e.g. `/product-images/<guid>.<ext>`) rather than physical disk paths.
  - Public static file endpoint (`GET /product-images/{filename}`) configured via ASP.NET Core `UseStaticFiles` without authentication requirement.
  - The storage abstraction allows substituting `LocalProductImageStorage` with Azure Blob Storage or S3 later without modifying domain logic or HTTP API contracts.
  - Storage path is configurable, facilitating Docker volume mounting (`/data/product-images`) in future deployment steps.
- **gRPC Stock Reservation Service**:
  - Language-neutral protobuf contract defined in `contracts/grpc/catalog_stock.proto`.
  - Operations: `ReserveStock`, `ReleaseReservation`, `CommitReservation`.
  - Dedicated internal HTTP/2 cleartext port (default `5059` in Development) coexisting with HTTP/1.1 REST (`5058`).
  - Concurrent-safe atomic inventory updates preventing race condition overselling.
  - Complete multi-item atomicity (all-or-nothing rollback on any item stock failure).
  - Idempotent request handling via unique `RequestId` constraint.
  - Three-state reservation lifecycle (`Reserved`, `Committed`, `Released`) with historical snapshot of product name and unit price (minor units).
  - Internal service-to-service communication only; never exposed publicly or via Gateway.
- **SignalR Realtime Catalog Updates**:
  - `CatalogHub` mapped to `/hubs/catalog`, publicly accessible (anonymous connections allowed).
  - Client receive-only model; mutation operations forbidden on the hub.
  - Broadcasts `ProductChanged` (on product creation, modification, and soft-deletion) and `StockChanged` (on admin stock adjustments, gRPC `ReserveStock`, `ReleaseReservation`, and `CancelCommittedReservation`).
  - Strict transaction boundary: all SignalR notifications are best-effort and dispatched strictly after database transaction commits; SignalR failures never rollback database changes.

### OrderService (partially implemented — Change-set №6)

- **JWT Authentication**: Independently validates JWT tokens issued by `IdentityService` using the symmetric signing key (`Jwt:Key`), validating Issuer, Audience, Lifetime, and Signing Key (`MapInboundClaims = false`, `NameClaimType = "sub"`).
- **Persistence Foundation**: Dedicated PostgreSQL database `shop_orders` with EF Core migrations `InitialOrders`, `AddCheckoutMetadata`, and `AddOrderCancellationState`.
- **Domain Entities**: `Cart`, `CartItem`, `Order`, `OrderItem`, `OrderStatusHistory`.
- **Checkout Metadata**: `Order.CheckoutRequestId` (unique index, idempotency key) and `Order.ReservationId` (unique index, catalog correlation).
- **Cancellation Metadata**: `Order.CancellationState` (`None`, `Pending`) stored as string via `HasConversion<string>()` with default `'None'`.
- **Enums**: `OrderStatus` (`Created`, `Confirmed`, `Processing`, `Shipped`, `Completed`, `Cancelled`), `PaymentStatus` (`Pending`, `Paid`, `Cancelled`), and `CancellationState` (`None`, `Pending`) stored as strings via `HasConversion<string>()`.
- **Database Constraints**: Unique index on `Cart.UserId` (one cart per user), unique composite index on `(CartId, ProductId)`, unique index on `Order.CheckoutRequestId`, unique index on `Order.ReservationId`, check constraints on `CartItem.Quantity > 0`, `OrderItem.Quantity > 0`, `OrderItem.UnitPrice >= 0`, `OrderItem.LineTotal >= 0`, `Order.TotalAmount >= 0`, decimal(18,2) money precision.
- **Authenticated Cart API**: Lazy cart creation, increment semantics on repeated item addition, absolute quantity update, item removal, and full cart clear.
- **Production gRPC Client**:
  - Connected to `CatalogService` internal HTTP/2 gRPC endpoint via `contracts/grpc/catalog_stock.proto`.
  - Abstraction: `ICatalogStockClient` with production implementation `CatalogStockGrpcClient`.
  - Configurable address (`CatalogGrpc:Address`) and timeout deadline (`CatalogGrpc:TimeoutSeconds`, default 5s).
  - Business errors (`OUT_OF_STOCK`, `PRODUCT_NOT_FOUND`, `PRODUCT_INACTIVE`, `CATEGORY_INACTIVE`) mapped to domain results and HTTP 409 Conflict.
  - Network/transport failures mapped to HTTP 503 (`CATALOG_UNAVAILABLE`) or HTTP 502 (`DOWNSTREAM_ERROR`).
- **Checkout Orchestration Pipeline**:
  - `POST /api/orders` with client-provided unique `RequestId`.
  - Idempotent fast-path: if `CheckoutRequestId` exists for current user, retries `CommitReservation` and returns 200 OK with existing order.
  - Empty cart rejection (400 Bad Request `EMPTY_CART`).
  - Immutable cart snapshot (max 100 items).
  - Stock reservation via Catalog gRPC `ReserveStock`.
  - Concurrent cart modification verification: if cart changed during reserve, cancels checkout, releases reservation, and returns 409 Conflict `CART_CHANGED`.
  - Atomic local transaction: persists `Order`, immutable `OrderItem` snapshots (name, unit price, quantity, line total), initial `OrderStatusHistory` ("Created"), and clears cart items.
  - Post-persistence confirmation: calls Catalog gRPC `CommitReservation`.
- **Strict Reservation Compensation Rules**:
  - **Before durable Order persistence**: any failure (cart modified concurrently, local DB error) triggers automatic `ReleaseReservation(reservationId)` compensation to restore stock immediately.
  - **After durable Order persistence**: `ReleaseReservation` is **strictly prohibited**. The order is durable. If `CommitReservation` fails (network error / timeout), the client receives HTTP 503 `CATALOG_UNAVAILABLE` and can safely retry checkout with the same `RequestId`; the fast-path will retry `CommitReservation`.
- **Order Read API & User Isolation**:
  - `GET /api/orders`: paginated list of current customer's orders sorted by `CreatedAtUtc DESC`.
  - `GET /api/orders/{id}`: returns full order details, snapshot items, and status history. Returns 404 Not Found if the order belongs to another customer (preventing ID enumeration).
- **Simulated Payment**:
  - `POST /api/orders/{id}/pay` (Owner only).
  - Transitions `PaymentStatus` from `Pending` to `Paid`, and `OrderStatus` from `Created` to `Confirmed`.
  - Idempotent on repeated calls; rejects cancelled orders with 409 Conflict `PAYMENT_CANCELLED`.
  - Rejects orders undergoing cancellation with 409 Conflict `ORDER_CANCELLATION_IN_PROGRESS`.
  - No external payment provider integrated.
- **Order Lifecycle & State Machine**:
  - `Created → Confirmed → Processing → Shipped → Completed` (terminal).
  - Cancellations allowed from `Created`, `Confirmed`, `Processing` to `Cancelled` (terminal).
  - Cancellations forbidden once `Shipped` or `Completed`.
- **3-Phase Durable Cancellation Orchestration & Stock Restoration**:
  - Customer (`POST /api/orders/{id}/cancel`) and Admin (`POST /api/admin/orders/{id}/cancel`).
  - **Phase A (Local Durable Intent)**: In an isolated local transaction under row lock (`FOR UPDATE`), checks `CanCancel` and durably persists `Order.CancellationState = CancellationState.Pending`.
  - **Phase B (Catalog Stock Return)**: Calls Catalog gRPC `CancelCommittedReservation(ReservationId)` to safely restore stock in `shop_catalog`. If Catalog is unreachable, returns 503 Service Unavailable while order remains `Pending`.
  - **Phase C (Local Finalization)**: In a new local transaction under row lock, sets `OrderStatus = Cancelled`, `PaymentStatus = Cancelled`, `CancellationState = None`, and records one `OrderStatusHistory` entry.
  - **Failure Recovery**: If local finalization in Phase C fails, `CancellationState` remains durable `Pending`. Concurrent Admin fulfillment (`Processing → Shipped`) and payment (`POST /pay`) are blocked with HTTP 409 `ORDER_CANCELLATION_IN_PROGRESS`. Retrying cancel performs an idempotent Catalog call and completes Phase C finalization.
- **Admin Order Management**:
  - `GET /api/admin/orders`: paginated list with optional status/paymentStatus filtering.
  - `GET /api/admin/orders/{id}`: full details including `UserId`.
  - `PATCH /api/admin/orders/{id}/status`: advances lifecycle (`Confirmed → Processing`, `Processing → Shipped`, `Shipped → Completed`). Generic status PATCH cannot cancel orders. Disallowed when `CancellationState == Pending` (returns 409 `ORDER_CANCELLATION_IN_PROGRESS`).
  - `POST /api/admin/orders/{id}/cancel`: cancels order without owner check using 3-phase orchestration.
  - Zero direct access to Identity DB (`shop_identity`) or Catalog DB (`shop_catalog`).
- **SignalR Realtime Order Updates**:
  - `OrderHub` mapped to `/hubs/orders`, requiring authentication (`[Authorize]`).
  - Supports JWT authentication over WebSocket via `access_token` query parameter, restricted strictly to `/hubs/orders`.
  - Automatic group management on connection: partitions connections into `user:{userId}` (extracted securely from token `sub` claim) and `admins` (for users with `Admin` role claim). Client-side group manipulation is forbidden.
  - Dispatches `OrderCreated` (after successful checkout and Catalog stock reservation confirmation), `OrderStatusChanged` (lifecycle progression, payment confirmation, cancellation), and `PaymentStatusChanged` (payment simulation, cancellation).
  - Strict transaction boundary: all SignalR notifications are dispatched strictly after local database transaction commits; failures are logged and never rollback business data.
- **Not yet implemented**: YARP API Gateway, Blazor WebAssembly frontend, Docker/Docker Compose.

## Technologies Intentionally Excluded in v1

- RabbitMQ
- Kafka
- Redis
- Kubernetes
- Event Sourcing
- MediatR
- AutoMapper
- CQRS
- MassTransit

