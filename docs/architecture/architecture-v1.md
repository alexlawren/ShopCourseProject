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

| Service | Database |
|---|---|
| `Shop.IdentityService` | `shop_identity` |
| `Shop.CatalogService` | `shop_catalog` |
| `Shop.OrderService` | `shop_orders` |

## Architecture Rules

- Each service is the sole owner of its data.
- Direct access to another service's tables is prohibited.
- The central business process is order processing.
- `Shop.OrderService` uses `Shop.CatalogService` via gRPC for stock reservation.
- No direct project references between `IdentityService`, `CatalogService`, and `OrderService`.
- `Shop.Web` does not reference any backend project directly.

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
