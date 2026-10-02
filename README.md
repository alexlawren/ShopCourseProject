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
- Identity and authentication.
- Catalog database model.
- Catalog public read API.
- Search/filter/sort/pagination.
- JWT validation in CatalogService.
- Admin-only Catalog write API.
- Category/Product soft delete.
- Stock management.
- Product image upload.
- JPEG/PNG/WEBP validation.
- Local image storage.
- Image replacement/removal.
- Public image serving.

Planned / not implemented:

- Azure Blob/cloud storage adapter.
- Stock reservation (gRPC).
- OrderService domain & processing.
- gRPC.
- SignalR.
- Gateway.
- Blazor UI.
- Docker Compose.


