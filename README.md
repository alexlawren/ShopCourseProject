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

Planned / not implemented:

- Blazor WebAssembly.
- Docker Compose.
- Final end-to-end validation.


