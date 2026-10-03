# Architecture v1

## Components

| Component              | Type                 | Description                                                |
| ---------------------- | -------------------- | ---------------------------------------------------------- |
| `Shop.Web`             | Blazor WebAssembly   | Web-client for users and administrators                    |
| `Shop.Gateway`         | ASP.NET Core         | Single entry point; YARP-based API Gateway                 |
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

### Gateway (implemented — Change-set №7)

- **Technology**: ASP.NET Core with `Yarp.ReverseProxy` (2.3.0).
- **Single Entry Point**: Unified external gateway for all external REST, static product image, and SignalR WebSocket traffic.
- **Path-Based Routing**:
  - `/api/auth/{**catch-all}` → `IdentityService` (`http://localhost:5078/`)
  - `/api/catalog/{**catch-all}` → `CatalogService` (`http://localhost:5058/`)
  - `/product-images/{**catch-all}` → `CatalogService` (`http://localhost:5058/`)
  - `/api/cart` and `/api/cart/{**catch-all}` → `OrderService` (`http://localhost:5141/`)
  - `/api/orders` and `/api/orders/{**catch-all}` → `OrderService` (`http://localhost:5141/`)
  - `/api/admin/orders` and `/api/admin/orders/{**catch-all}` → `OrderService` (`http://localhost:5141/`)
  - `/hubs/catalog` and `/hubs/catalog/{**catch-all}` → `CatalogService` (`http://localhost:5058/`)
  - `/hubs/orders` and `/hubs/orders/{**catch-all}` → `OrderService` (`http://localhost:5141/`)
- **Transparent Security Boundary**: Gateway does not validate or duplicate JWT authorization; downstream services remain the security boundary and validate JWT tokens independently. `Authorization: Bearer <token>` header is forwarded transparently.
- **WebSocket Upgrade Support**: Seamless WebSocket proxying for SignalR hubs with query string token preservation (`access_token`).
- **Internal Service Isolation**: CatalogService internal gRPC endpoint (port 5059) is **strictly not exposed** through the Gateway. OrderService connects to CatalogService gRPC directly.
- **Gateway Health**: Dedicated `/health` probe endpoint returning `200 OK`.
- **CORS Configuration**: Restricts access to specific `Shop.Web` development origins (`http://localhost:5287`, `https://localhost:7177`) with explicit methods and headers, without wildcard credentials.

### Shop.Web (Customer & Admin Commerce UI Implemented — Change-set №8B.1 & №8B.2)

- **Technology**: Blazor WebAssembly (.NET 9.0) client-side Single Page Application.
- **Gateway-Only Networking**: Browser connects strictly and exclusively to `Shop.Gateway` (`http://localhost:5210` in Development) configured via `Gateway:BaseUrl`. Direct browser calls to downstream microservices (5078, 5058, 5059, 5141) are strictly prohibited.
- **Authentication & Token Storage**:
  - `ITokenStorage` backed by browser `sessionStorage` via minimal JS interop. Tokens persist across page reloads (F5) within a tab and are cleared on tab close.
  - `CustomAuthenticationStateProvider` decodes Base64Url JWT claims (`sub`, `email`, `role`) for UI rendering only. Backend microservices remain the sole authoritative security validators.
  - No JWT signing keys or database credentials exist on the frontend.
- **Safe Mutation Refresh Token Flow**:
  - Proactive JWT expiration check (`JwtClaimsParser.IsExpiredOrExpiringSoon`, 30s skew) triggers `RefreshAsync()` before sending mutations (POST/PUT/DELETE/PATCH) to prevent replaying unsafe calls on 401.
  - Concurrency protected via `SemaphoreSlim(1, 1)` with double-checked locking in `AuthService`.
  - Fallback 401 interceptor retry preserved strictly for safe GET requests.
  - On failure: tokens purged, active OrderHub connection terminated, UI transitions to anonymous.
- **Public Product Catalog**:
  - Paginated browsing with search, category filtering, price bounds, in-stock filter, and sorting.
  - Product details view (`/products/{id}`) with robust 404 Not Found handling.
  - Product images served exclusively through Gateway URL builder (`http://localhost:5210/product-images/...`).
  - Add to Cart directly from catalog card and details page with quantity stepper.
- **Catalog Realtime Integration**:
  - SignalR client connects to Gateway `/hubs/catalog` with automatic reconnection.
  - `StockChanged` events update visible product stock in-place.
  - `ProductChanged` events trigger catalog refetch, immediately removing soft-deleted items.
- **Customer Commerce Flow (Catalog → Cart → Checkout → Orders)**:
  - **Cart UI (`/cart`)**: Enriches OrderService `(ProductId, Quantity)` entries with CatalogService metadata (`Task.WhenAll`). Displays unavailable item alerts. Absolute quantity PUT (1..1000), item removal DELETE, clear cart with confirmation. Decimal preview total calculation.
  - **Checkout UI**: Generates client `Guid RequestId` saved in `sessionStorage` (`pending_checkout_request_id`). Preserved for retry upon uncertain failures (502, 503, network drop). Cleared upon definitive order creation (201/200) or deterministic business rejection (`OUT_OF_STOCK`, `CART_CHANGED`, `EMPTY_CART`).
  - **Orders UI (`/orders`)**: Paginated list of customer orders with status/payment badges and "Подробнее" link.
  - **Order Details (`/orders/{id}`)**: Historical snapshot line items (`ProductName`, `UnitPrice`, `Quantity`, `LineTotal`), chronological `OrderStatusHistory` timeline, strict user ownership isolation.
  - **Simulated Payment UI**: Triggers `POST /api/orders/{id}/pay` for pending orders with educational disclaimer.
  - **Order Cancellation UI**: Permitted only for cancellable statuses (`Created`, `Confirmed`, `Processing`). Native confirmation dialog. Handles `ORDER_CANCELLATION_IN_PROGRESS` gracefully.
- **OrderHub SignalR Realtime Integration**:
  - Connects to Gateway `/hubs/orders` via WebSocket.
  - Dynamic `AccessTokenProvider` providing up-to-date JWT from `ITokenStorage` on connect/reconnect.
  - Scoped lifecycle tied to SPA session; disconnected on logout.
  - Subscribes to `OrderCreated`, `OrderStatusChanged`, `PaymentStatusChanged`, updating list and details views idempotently.
  - Admin connections automatically join the `admins` group to receive global order creation and transition events.
- **Admin Commerce Management UI (Change-set №8B.2)**:
  - **Admin Navigation & Guarding**: Route protection via `@attribute [Authorize(Roles = "Admin")]` and `AuthorizeRouteView` in `App.razor`. Non-admin customers encounter a 403 Forbidden alert.
  - **Admin Dashboard (`/admin`)**: Central portal for catalog and order management.
  - **Admin Catalog Management (`/admin/catalog`)**:
    - Dedicated backend read API (`GET /api/catalog/admin/categories` and `GET /api/catalog/admin/products`) allowing visibility into inactive categories and soft-deleted products.
    - Category CRUD: creation with slug validation regex contract, editing, soft-delete deactivation, and reactivation (`IsActive = true`).
    - Product CRUD: creation with validation, editing (excluding direct stock/image mutation), soft-deletion, and reactivation.
    - Dedicated Stock Adjustment: setting absolute inventory (`PATCH /api/catalog/products/{id}/stock`).
    - Product Image Management: client-validated `InputFile` (JPEG/PNG/WEBP, 5 MiB max) streaming multipart form-data to `POST /api/catalog/products/{id}/image`; static serving through Gateway; deletion via `DELETE /api/catalog/products/{id}/image`.
    - SignalR `CatalogHub` live updates for inventory and product state changes.
  - **Admin Orders Management (`/admin/orders`, `/admin/orders/{id}`)**:
    - Orders list displaying Order ID, Customer `UserId` (GUID only; no IdentityService user email lookup), timestamps, status badges, and total amount.
    - Filtering by order status and payment status with server-side pagination.
    - Order details showing immutable historical `OrderItem` snapshots and full `OrderStatusHistory` timeline.
    - Guided lifecycle transitions (`Created → Confirmed → Processing → Shipped → Completed`) via `PATCH /api/admin/orders/{id}/status`.
    - Admin Cancellation (`POST /api/admin/orders/{id}/cancel`): permitted for `Created`, `Confirmed`, and `Processing` orders; triggers inventory return via Catalog gRPC; disabled for `Shipped`, `Completed`, and `Cancelled`.
    - Real-time order sync via OrderHub `admins` group subscription.
- **Containerization & Orchestration Architecture (Change-set №9)**:
  - **Docker Compose Topology**: 5 services (`postgres`, `identity-service`, `catalog-service`, `order-service`, `gateway`) connected in an isolated bridge network (`shop-network`).
  - **Single External Origin**: Only `gateway` exposes an external host port (`8080:8080`). All internal microservices and PostgreSQL do not publish host ports.
  - **Embedded Static Web Hosting**: `Shop.Gateway` embeds Blazor WebAssembly static assets (`/app/wwwroot`), acting simultaneously as reverse proxy (YARP) and web host. Eliminates extra reverse proxies (Nginx) and cross-origin complexity for the SPA.
  - **Direct Intra-Cluster gRPC**: `order-service` calls `catalog-service` gRPC on `http://catalog-service:8081` directly within the bridge network, bypassing the gateway.
  - **State Persistence**: Named volumes `postgres-data` (database files) and `product-images` (uploaded catalog photos).
  - **Automated Lifecycle & Migrations**: PostgreSQL initialization scripts (`init-databases.sql`) provision separate databases (`shop_identity`, `shop_catalog`, `shop_orders`). Services automatically execute EF Core migrations and admin user bootstrapping on container startup.
- **Deferred to Future Stages**:
  - Final E2E testing suite.
  - Architecture and UML diagrams.
  - Coursework final report.

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

