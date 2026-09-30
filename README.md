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
- PostgreSQL/EF Core persistence foundation.
- IdentityService user registration/authentication.
- JWT access tokens.
- Refresh token rotation.
- Customer/Admin role infrastructure.
- Initial Identity database migration.

Planned / not implemented:

- Catalog domain.
- Order domain.
- gRPC inter-service communication.
- Gateway routing (YARP).
- SignalR real-time updates.
- Web shop UI.
- Docker Compose.
