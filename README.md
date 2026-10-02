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
- Release (inventory restoration) and Commit (permanent confirmation) semantics.
- OrderService production Catalog gRPC client (`ICatalogStockClient`, `CatalogStockGrpcClient`) via shared `contracts/grpc/catalog_stock.proto`.
- Checkout pipeline (`POST /api/orders`) with client-generated idempotent `RequestId`.
- Order creation with immutable `OrderItem` snapshots (name, unit price, quantity, line total) and initial `OrderStatusHistory` ("Created").
- Atomic local database transaction ensuring order persistence and cart clearance commit together.
- Inter-service reservation orchestration (Reserve -> local save -> Commit, with automatic Release compensation prior to durable persistence).
- Customer order read API (`GET /api/orders`, `GET /api/orders/{id}`) with strict per-user ownership isolation.

Planned / not implemented:

- Simulated payment processing.
- Order processing lifecycle and status management API.
- Order cancellation with inventory restoration.
- Admin order management API.
- SignalR realtime notifications.
- YARP API Gateway.
- Blazor WebAssembly UI.
- Docker & Docker Compose setup.

