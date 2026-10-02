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

```
Browser
  └─► Shop.Gateway          HTTPS / REST / JSON
        ├─► Shop.IdentityService   HTTP / REST
        ├─► Shop.CatalogService    HTTP / REST
        └─► Shop.OrderService      HTTP / REST

Shop.OrderService ──────────► Shop.CatalogService   gRPC (stock reservation)

Shop.CatalogService ─────────► Browser              SignalR / WebSocket (realtime)
Shop.OrderService   ─────────► Browser              SignalR / WebSocket (realtime)
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

### CatalogService (partially implemented — Change-set №4A, №4B.1 & №4B.2)

- PostgreSQL database `shop_catalog` with EF Core migration `InitialCatalog`.
- Domain entities: `Category`, `Product` with one-to-many relationship, `Restrict` delete behavior, and PostgreSQL check constraints.
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
- **Not yet implemented**: gRPC stock reservation, SignalR realtime updates.

### OrderService (partially implemented — Change-set №5A)

- **JWT Authentication**: Independently validates JWT tokens issued by `IdentityService` using the symmetric signing key (`Jwt:Key`), validating Issuer, Audience, Lifetime, and Signing Key (`MapInboundClaims = false`, `NameClaimType = "sub"`).
- **Persistence Foundation**: Dedicated PostgreSQL database `shop_orders` with EF Core migration `InitialOrders`.
- **Domain Entities**: `Cart`, `CartItem`, `Order`, `OrderItem`, `OrderStatusHistory`.
- **Enums**: `OrderStatus` (`Created`, `Confirmed`, `Processing`, `Shipped`, `Completed`, `Cancelled`) and `PaymentStatus` (`Pending`, `Paid`, `Cancelled`) stored as strings via `HasConversion<string>()`.
- **Database Constraints**: Unique index on `Cart.UserId` (one cart per user), unique composite index on `(CartId, ProductId)`, check constraints on `CartItem.Quantity > 0`, `OrderItem.Quantity > 0`, `OrderItem.UnitPrice >= 0`, `OrderItem.LineTotal >= 0`, `Order.TotalAmount >= 0`, decimal(18,2) money precision.
- **Authenticated Cart API**: Lazy cart creation, increment semantics on repeated item addition, absolute quantity update, item removal, and full cart clear.
- **Per-User Isolation**: User ID is resolved strictly from the validated JWT token (`sub` claim); users cannot view or manipulate carts of other users.
- **OrderItem Snapshot Model**: `OrderItem` stores historical snapshots of `ProductName` and `UnitPrice` to preserve historical integrity regardless of subsequent catalog changes.
- **Not yet implemented**: Checkout, order lifecycle and status management API, gRPC stock reservation with CatalogService, simulated payment, SignalR realtime notifications.

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

