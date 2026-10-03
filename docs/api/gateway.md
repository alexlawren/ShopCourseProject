# API Gateway (`Shop.Gateway`)

The API Gateway provides a unified external entry point for all client-facing requests in the `ShopCourseProject` system, implementing path-based reverse proxy routing, header/body forwarding, static file proxying, and WebSocket upgrade tunneling using **YARP (Yet Another Reverse Proxy)**.

---

## Architecture & Responsibilities

1. **Single External Entry Point**: Web clients (Blazor WebAssembly, mobile, external tools) connect exclusively to `Shop.Gateway`. Direct external access to downstream services is avoided.
2. **Reverse Proxy Only**: The Gateway contains zero business logic, zero entity models, zero database contexts (`DbContext`), and no gRPC clients.
3. **Transparent Security Boundary**: Downstream services (`IdentityService`, `CatalogService`, `OrderService`) maintain their own security boundary and validate JWT tokens, claims, and roles independently. The Gateway forwards the `Authorization: Bearer <token>` header transparently without duplicate authentication.
4. **SignalR & WebSocket Tunneling**: The Gateway proxies SignalR negotiation requests (`POST /hubs/.../negotiate`), upgrades WebSocket connections (`101 Switching Protocols`), and preserves query string parameters (including `access_token`).
5. **Static File Proxying**: Product images stored in `CatalogService` are served through the Gateway route `/product-images/{filename}` without file duplication.
6. **Internal Service-to-Service Isolation**: The internal gRPC endpoint of `CatalogService` (`StockReservationService` on port `5059`) is **strictly forbidden** from exposure through the Gateway. Downstream gRPC calls (`OrderService → CatalogService`) bypass the Gateway entirely.

---

## Gateway Endpoints & Routing Table

**Development Base URL**: `http://localhost:5210`

### Local Gateway Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/health` | `GET` | Gateway liveness probe (`{"status":"Healthy","service":"Gateway"}`). |

### Reverse Proxy Routes & Clusters

| Route ID | Path Pattern | Cluster ID | Downstream Destination | Description |
|---|---|---|---|---|
| `identity-route` | `/api/auth/{**catch-all}` | `identity-cluster` | `http://localhost:5078/` | Identity REST API (register, login, refresh, logout, me). |
| `catalog-route` | `/api/catalog/{**catch-all}` | `catalog-cluster` | `http://localhost:5058/` | Public catalog & Admin catalog CRUD/stock REST APIs. |
| `product-images-route` | `/product-images/{**catch-all}` | `catalog-cluster` | `http://localhost:5058/` | Static product image serving. |
| `catalog-hub-base` | `/hubs/catalog` | `catalog-cluster` | `http://localhost:5058/` | Public Catalog SignalR hub connection endpoint. |
| `catalog-hub-route` | `/hubs/catalog/{**catch-all}` | `catalog-cluster` | `http://localhost:5058/` | Catalog SignalR hub negotiation and WebSocket upgrade. |
| `cart-base` | `/api/cart` | `order-cluster` | `http://localhost:5141/` | Base cart endpoint (get cart, clear cart). |
| `cart-route` | `/api/cart/{**catch-all}` | `order-cluster` | `http://localhost:5141/` | Cart item management endpoints (`/api/cart/items`, etc.). |
| `orders-base` | `/api/orders` | `order-cluster` | `http://localhost:5141/` | Order creation (checkout) and customer orders list. |
| `orders-route` | `/api/orders/{**catch-all}` | `order-cluster` | `http://localhost:5141/` | Order details (`/api/orders/{id}`), pay, cancel. |
| `admin-orders-base` | `/api/admin/orders` | `order-cluster` | `http://localhost:5141/` | Admin orders list. |
| `admin-orders-route` | `/api/admin/orders/{**catch-all}` | `order-cluster` | `http://localhost:5141/` | Admin order details, status transitions, admin cancel. |
| `order-hub-base` | `/hubs/orders` | `order-cluster` | `http://localhost:5141/` | Authenticated Order SignalR hub connection endpoint. |
| `order-hub-route` | `/hubs/orders/{**catch-all}` | `order-cluster` | `http://localhost:5141/` | Order SignalR hub negotiation and WebSocket upgrade. |

---

## Configuration & Overrides

YARP routes and clusters are configured in `appsettings.json` under the `"ReverseProxy"` section. Destination addresses can be overridden via environment variables or Docker Compose:

```bash
# Cluster Destination Overrides
ReverseProxy__Clusters__identity-cluster__Destinations__identity-destination__Address=http://localhost:5078/
ReverseProxy__Clusters__catalog-cluster__Destinations__catalog-destination__Address=http://localhost:5058/
ReverseProxy__Clusters__order-cluster__Destinations__order-destination__Address=http://localhost:5141/
```

---

## Security & Error Handling

1. **Unknown Routes**: Requests matching no route rule receive `404 Not Found`. There is no default or catch-all route pointing to any downstream service.
2. **Downstream Unavailability**: If a downstream service is unreachable or stopped, YARP returns `502 Bad Gateway` (or `503 Service Unavailable`), while the Gateway process remains healthy and continues serving other routes and `/health`.
3. **CORS Policy**: Configured during Blazor UI integration (`Shop.Web`) once the client hosting model and origin are established.
