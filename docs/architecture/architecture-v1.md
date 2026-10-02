# Architecture v1

## Components

| Component | Type | Description |
|---|---|---|
| `Shop.Web` | Blazor WebAssembly | Web-client for users and administrators |
| `Shop.Gateway` | ASP.NET Core | Single entry point; YARP-based API Gateway (planned) |
| `Shop.IdentityService` | ASP.NET Core Web API | Users, roles (Customer/Admin), JWT, refresh tokens |
| `Shop.CatalogService` | ASP.NET Core Web API | Categories, products, search, stock reservation |
| `Shop.OrderService` | ASP.NET Core Web API | Cart, order processing, order statuses, payment simulation |

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

| Service | DbContext | Database |
|---|---|---|
| `Shop.IdentityService` | `IdentityDbContext` | `shop_identity` |
| `Shop.CatalogService` | `CatalogDbContext` | `shop_catalog` |
| `Shop.OrderService` | `OrderDbContext` | `shop_orders` |

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

### CatalogService (partially implemented — Change-set №4A & №4B.1)

- PostgreSQL database `shop_catalog` with EF Core migration `InitialCatalog`.
- Domain entities: `Category`, `Product` with one-to-many relationship, `Restrict` delete behavior, and PostgreSQL check constraints.
- Public read API: categories list, products list, product details by ID (accessible without authentication).
- Server-side features: case-insensitive search (`EF.Functions.ILike`), filtering (categoryId, minPrice, maxPrice, inStock), sorting (`priceAsc`, `priceDesc`, `nameAsc`, `nameDesc`, `newest`), and pagination (max pageSize = 100).
- Standard ASP.NET Core `ProblemDetails` error responses.
- **JWT Authorization**: CatalogService independently validates JWT tokens issued by `IdentityService` using the symmetric signing key (`Jwt:Key`), validating Issuer, Audience, Lifetime, and Signing Key without querying `shop_identity` or using `IdentityDbContext`.
- **Admin Write API**: Admin CRUD endpoints for categories and products (`POST /api/catalog/categories`, `PUT /api/catalog/categories/{id}`, `DELETE /api/catalog/categories/{id}`, `POST /api/catalog/products`, `PUT /api/catalog/products/{id}`, `DELETE /api/catalog/products/{id}`).
- **Soft Deletion**: Category and product deletion is strictly soft-delete (`IsActive = false`, `UpdatedAtUtc = UtcNow`) preserving audit history and relational integrity. Inactive categories hide their associated products from public read queries.
- **Stock Management**: Absolute stock level adjustments via `PATCH /api/catalog/products/{id}/stock` with non-negative validation and PostgreSQL check constraint enforcement.
- **Not yet implemented**: Product image upload/filesystem storage (planned for Change-set №4B.2), gRPC stock reservation, SignalR realtime updates.

### OrderService (skeleton only)

- DbContext registered, no domain entities or migrations yet.

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
