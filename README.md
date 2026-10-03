# ShopCourseProject

Курсовой проект по дисциплине «Программирование сетевых приложений».

## Тема

**Сетевое клиент-серверное приложение для обработки заказов в интернет-магазине.**

## Назначение

Проект предназначен для разработки клиент-серверной системы интернет-магазина с централизованным процессом обработки заказов.

Планируемая система включает web-клиент, API Gateway и несколько серверных сервисов с разделением ответственности.

## Planned technology stack

- C#
- .NET 9
- ASP.NET Core
- Blazor WebAssembly
- PostgreSQL
- Entity Framework Core
- REST / HTTPS
- gRPC
- SignalR / WebSocket
- JWT authentication
- Docker
- Docker Compose
- xUnit

## Planned architecture

Основные компоненты:

- `Shop.Web` — web-клиент;
- `Shop.Gateway` — единая точка входа в серверную часть;
- `Shop.IdentityService` — пользователи, аутентификация и авторизация;
- `Shop.CatalogService` — товары, категории и складские остатки;
- `Shop.OrderService` — корзина и обработка заказов.

Предполагается использование:

- REST/HTTPS для взаимодействия клиента с серверной системой;
- gRPC для межсервисного взаимодействия `OrderService → CatalogService`;
- SignalR/WebSocket для обновлений в реальном времени;
- PostgreSQL для хранения данных.

## Repository branches

- `main` — стабильное состояние проекта;
- `coursework/dev` — основная ветка разработки.

## Current status

Implemented:

- Solution skeleton.
- Persistence foundation.
- Identity and authentication (`shop_identity`, JWT, refresh tokens).
- Catalog database model and public read API (`shop_catalog`, search/filter/sort/pagination).
- JWT validation in CatalogService and Admin write API (CRUD, soft delete, stock management).
- Product image upload, validation, local filesystem storage, and public static image serving.
- Order persistence foundation (`shop_orders`, `InitialOrders` migration).
- Order domain model (`Cart`, `CartItem`, `Order`, `OrderItem`, `OrderStatusHistory`, `OrderStatus`, `PaymentStatus`).
- Authenticated Cart API (`GET /api/cart`, `POST /api/cart/items`, `PUT /api/cart/items/{productId}`, `DELETE /api/cart/items/{productId}`, `DELETE /api/cart`).
- Per-user cart isolation based on JWT `sub` claim.
- Catalog gRPC StockReservation service (`contracts/grpc/catalog_stock.proto`, internal HTTP/2 endpoint).
- Atomic stock reservation with concurrent-safe updates and multi-item rollback.
- Stock reservation persistence (`shop_catalog`, `AddStockReservations` migration).
- Idempotent request handling via unique `RequestId`.
- Release (inventory restoration), Commit (permanent confirmation), and CancelCommitted (restoring committed stock) semantics.
- OrderService production Catalog gRPC client (`ICatalogStockClient`, `CatalogStockGrpcClient`) via shared `contracts/grpc/catalog_stock.proto`.
- Checkout pipeline (`POST /api/orders`) with client-generated idempotent `RequestId`.
- Order creation with immutable `OrderItem` snapshots (name, unit price, quantity, line total) and initial `OrderStatusHistory` ("Created").
- Atomic local database transaction ensuring order persistence and cart clearance commit together.
- Inter-service reservation orchestration (Reserve -> local save -> Commit, with automatic Release compensation prior to durable persistence).
- Customer order read API (`GET /api/orders`, `GET /api/orders/{id}`) with strict per-user ownership isolation.
- Simulated payment processing (`POST /api/orders/{id}/pay`) transitioning `Created → Confirmed` and `Pending → Paid`.
- Complete order lifecycle state machine (`Created → Confirmed → Processing → Shipped → Completed`) with full `OrderStatusHistory` auditing.
- Customer cancellation (`POST /api/orders/{id}/cancel`) and Admin cancellation (`POST /api/admin/orders/{id}/cancel`).
- Safe committed inventory return via Catalog gRPC `CancelCommittedReservation` with 3-phase durable cancellation orchestration (`CancellationState.Pending`), preventing post-Catalog race conditions against Admin status updates (`ORDER_CANCELLATION_IN_PROGRESS`) and enabling idempotent failure recovery.
- Admin order management API (`GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `PATCH /api/admin/orders/{id}/status`, `POST /api/admin/orders/{id}/cancel`).
- SignalR OrderHub (`/hubs/orders`).
- Authenticated realtime user groups (`user:{userId}`).
- Admin realtime group (`admins`).
- Real-time order events (`OrderCreated`, `OrderStatusChanged`, `PaymentStatusChanged`).
- SignalR CatalogHub (`/hubs/catalog`, public receive-only).
- Real-time catalog events (`ProductChanged`, `StockChanged` on Admin adjustments, gRPC reserve, release, cancel committed).
- WebSocket smoke verification.
- YARP API Gateway (`Shop.Gateway`).
- Unified external entry point for REST, static product images, and SignalR WebSocket.
- Identity REST routing (`/api/auth/*`).
- Catalog REST and static product images routing (`/api/catalog/*`, `/product-images/*`).
- Order, Cart, and Admin Order REST routing (`/api/cart*`, `/api/orders*`, `/api/admin/orders*`).
- SignalR WebSocket proxying (`/hubs/orders*`, `/hubs/catalog*`) with access token preservation.
- Gateway health endpoint (`GET /health`).
- Blazor WebAssembly UI foundation (`Shop.Web`).
- Gateway API client (Gateway-only networking via `http://localhost:5210`).
- User registration, login, and logout UI.
- JWT authentication state provider (presentation-only claim parsing).
- Refresh-token flow with concurrency lock and retry.
- Session storage token persistence (`sessionStorage` via JS interop).
- Public product catalog UI with search, category filtering, min/max price, in-stock filter, and sorting.
- Server-side catalog pagination.
- Product details page with image display through Gateway and 404 handling.
- Catalog SignalR realtime updates (`StockChanged` in-place update, `ProductChanged` auto-refetch).
- Role-aware navigation bar (Customer/Admin role indicators).
- Customer Cart UI (`/cart`) with Catalog product enrichment (`Task.WhenAll`), quantity controls (1..1000), removal, clear cart confirmation, and decimal preview total calculation.
- Add to Cart directly from catalog listing and product details pages.
- Checkout UI (`POST /api/orders`) with client-generated idempotent `RequestId`.
- Checkout idempotency recovery (`sessionStorage` persistence; preserve on uncertain 502/503/timeout, clear on definitive 201/200 and deterministic business rejections).
- Customer Orders UI (`/orders`) with server-side pagination, status/payment badges, and details navigation.
- Order details UI (`/orders/{id}`) with immutable historical `OrderItem` snapshots (name, unit price, quantity, line total) and chronological `OrderStatusHistory` timeline.
- Payment simulation UI (`POST /api/orders/{id}/pay`) with educational disclaimer and double-submit protection.
- Order cancellation UI (`POST /api/orders/{id}/cancel`) with native confirmation dialog and `ORDER_CANCELLATION_IN_PROGRESS` retry handling.
- OrderHub SignalR realtime customer updates (`/hubs/orders` via Gateway, dynamic JWT `AccessTokenProvider`, automatic reconnect, customer group isolation).
- Safe mutation token refresh (proactive expiration check with 30s skew before sending mutations; retry strictly limited to GET).
- Admin Commerce UI (`Shop.Web`):
  - Admin dashboard (`/admin`) and role-aware navigation and route authorization.
  - Admin Catalog UI (`/admin/catalog`): Category management (create, edit, soft-delete, reactivation, slug regex validation), Product management (search/category/active filters, create, edit, soft-delete, reactivation), Stock management (`PATCH /stock`), Product image administration (`InputFile` upload, Gateway static image serving, image deletion).
  - Minimal Admin Catalog Read API (`GET /api/catalog/admin/categories`, `GET /api/catalog/admin/products`) supporting inactive category and product visibility.
  - Admin Orders UI (`/admin/orders`): Orders list with customer `UserId` (no IdentityService lookup), status and payment status filters, server-side pagination.
  - Admin Order Details (`/admin/orders/{id}`): Historical snapshot line items, chronological `OrderStatusHistory` timeline.
  - Lifecycle controls (guided forward state progression `Created → Confirmed → Processing → Shipped → Completed`).
  - Admin cancellation (`POST /api/admin/orders/{id}/cancel`) with committed stock return, cancellation state protection, and retry support.
  - Real-time Admin updates via SignalR (`CatalogHub` stock/product updates, `OrderHub` `admins` group for order creation and state transitions).

Planned / not implemented:

- Docker Compose.
- Final end-to-end validation.
- Architecture and UML diagrams.
- Coursework final report.




