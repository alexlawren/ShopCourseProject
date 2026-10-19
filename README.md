# ShopCourseProject

Курсовой проект по дисциплине «Программирование сетевых приложений».

**Тема:** Сетевое клиент-серверное приложение для обработки заказов в интернет-магазине.

---

## Возможности

### Для покупателя

- Регистрация и авторизация (JWT + refresh tokens).
- Просмотр каталога товаров с поиском, фильтрацией по категории, диапазону цен и наличию, а также сортировкой.
- Страница товара с изображением.
- Корзина: добавление, изменение количества, удаление позиций.
- Оформление заказа с идемпотентностью (client-generated `RequestId`).
- Просмотр списка своих заказов с пагинацией.
- Страница деталей заказа: снимки позиций на момент оформления, история статусов.
- Симуляция оплаты.
- Отмена заказа (в допустимых состояниях).
- Realtime-обновления статусов заказов через SignalR.

### Для администратора

- Управление категориями: создание, редактирование, мягкое удаление, восстановление, валидация slug.
- Управление товарами: создание, редактирование, мягкое удаление, восстановление, фильтрация по статусу.
- Управление складскими остатками (`PATCH /stock`).
- Управление изображениями товаров: загрузка, удаление, отображение через Gateway.
- Просмотр всех заказов с фильтрацией по статусу и оплате, пагинация.
- Страница деталей заказа: снимки позиций и история статусов.
- Управление жизненным циклом заказа (`Created → Confirmed → Processing → Shipped → Completed`).
- Отмена заказа с возвратом зарезервированных остатков.
- Realtime-обновления каталога и заказов через SignalR.

---

## Архитектура

```
Браузер
   ↓
Blazor WebAssembly (Shop.Web)
   ↓ HTTP/WebSocket
YARP API Gateway (Shop.Gateway) — единая внешняя точка входа
   ├─ REST/JSON ──────────────→ Shop.IdentityService
   ├─ REST/JSON ──────────────→ Shop.CatalogService (HTTP/1.1)
   ├─ REST/JSON ──────────────→ Shop.OrderService
   ├─ Static files ───────────→ Shop.CatalogService (product-images)
   └─ SignalR/WebSocket ──────→ Shop.CatalogService, Shop.OrderService
                                         ↓ gRPC/HTTP2 (внутри docker-сети)
                              Shop.CatalogService ←── Shop.OrderService
                                         ↓
                                    PostgreSQL
```

**Протоколы:**

| Слой | Протокол |
|---|---|
| Внешний API (клиент ↔ Gateway) | REST/HTTP + JSON |
| Order Service ↔ Catalog Service | gRPC / HTTP2 (внутри Docker-сети) |
| Realtime-уведомления | SignalR / WebSocket |
| Аутентификация | JWT (Bearer) |
| Хранение данных | PostgreSQL |

---

## Технологии

- **Язык:** C# (.NET 9)
- **Web API:** ASP.NET Core
- **UI:** Blazor WebAssembly
- **API Gateway:** YARP (Yet Another Reverse Proxy)
- **ORM:** Entity Framework Core + Npgsql
- **База данных:** PostgreSQL 17
- **Межсервисная коммуникация:** gRPC (Google.Protobuf, Grpc.AspNetCore)
- **Realtime:** SignalR
- **Контейнеризация:** Docker, Docker Compose
- **Тестирование:** xUnit

---

## Структура проекта

```
ShopCourseProject/
├── src/
│   ├── Shop.Web/                  # Blazor WebAssembly — UI клиент
│   ├── Shop.Gateway/              # YARP API Gateway
│   └── Services/
│       ├── Shop.IdentityService/  # Пользователи, аутентификация, JWT
│       ├── Shop.CatalogService/   # Товары, категории, остатки, изображения
│       └── Shop.OrderService/     # Корзина, заказы, оплата
├── tests/
│   ├── Shop.IdentityService.Tests/
│   ├── Shop.CatalogService.Tests/
│   ├── Shop.OrderService.Tests/
│   ├── Shop.Web.Tests/
│   └── Shop.IntegrationTests/
├── contracts/
│   └── grpc/                      # Protobuf-контракт CatalogStock gRPC
├── deploy/
│   ├── docker/                    # Многоэтапные Dockerfile для каждого сервиса
│   └── postgres/                  # SQL-скрипты инициализации БД
├── docs/                          # Документация (см. раздел «Документация»)
├── docker-compose.yml
├── .env.example
├── .editorconfig
├── .gitignore
└── ShopCourseProject.sln
```

---

## Запуск через Docker Compose

Это основной и рекомендуемый способ запуска проекта.

### 1. Клонировать репозиторий

```bash
git clone https://github.com/alexlawren/ShopCourseProject.git
cd ShopCourseProject
```

### 2. Создать файл `.env`

**Linux / macOS:**
```bash
cp .env.example .env
```

**Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
```

### 3. Заполнить переменные окружения

Открыть `.env` и задать реальные значения:

```dotenv
POSTGRES_USER=shop
POSTGRES_PASSWORD=<ваш_пароль_postgresql>

# Не менее 32 символов (HMAC-SHA256)
JWT_KEY=<ваш_jwt_секретный_ключ_минимум_32_символа>

# Внешний порт Gateway (по умолчанию 8080)
GATEWAY_PORT=8080

# Bootstrap-администратор (создаётся при первом запуске)
BOOTSTRAP_ADMIN_EMAIL=admin@shop.local
BOOTSTRAP_ADMIN_PASSWORD=<пароль_администратора>
```

> **Важно:** никогда не коммитьте файл `.env` в репозиторий. Он находится в `.gitignore`.

### 4. Запустить комплекс сервисов

```bash
docker compose up -d --build
```

При первом запуске будут собраны Docker-образы, выполнены миграции EF Core и создан bootstrap-администратор.

### 5. Проверить состояние контейнеров

```bash
docker compose ps
```

Ожидаемый результат — все 5 контейнеров в состоянии `running`:

| Контейнер | Назначение |
|---|---|
| `postgres` | PostgreSQL 17 |
| `identity-service` | Аутентификация и управление пользователями |
| `catalog-service` | Каталог товаров и складские остатки |
| `order-service` | Корзина и заказы |
| `gateway` | YARP API Gateway + Blazor WebAssembly |

### 6. Открыть приложение

```
http://localhost:8080
```

*(Если в `.env` задан `GATEWAY_PORT`, отличный от `8080`, использовать соответствующий порт.)*

### 7. Остановить комплекс

```bash
docker compose down
```

---

## Администратор

При первом запуске `Shop.IdentityService` проверяет переменные окружения `BOOTSTRAP_ADMIN_EMAIL` и `BOOTSTRAP_ADMIN_PASSWORD`. Если они заданы, сервис автоматически создаёт учётную запись с ролью `Admin` (или назначает роль существующему пользователю).

Войти в административный раздел можно по адресу `http://localhost:8080` после авторизации с указанными учётными данными.

---

## Тестирование

```bash
dotnet test ShopCourseProject.sln
```

Результат последнего прогона:

| Проект | Пройдено | Не пройдено | Пропущено |
|---|---|---|---|
| Shop.Web.Tests | 182 | 0 | 0 |
| Shop.CatalogService.Tests | 155 | 0 | 0 |
| Shop.OrderService.Tests | 97 | 0 | 0 |
| Shop.IdentityService.Tests | 11 | 0 | 0 |
| Shop.IntegrationTests | 9 | 0 | 0 |
| **Итого** | **454** | **0** | **0** |

---

## Docker

Сервисы запускаются в изолированной Docker-сети `shop-network`. Единственная внешняя точка входа — `gateway` на порту `${GATEWAY_PORT:-8080}`. Внутренние сервисы не имеют открытых портов на хосте.

**Именованные Docker-тома:**

| Том | Назначение |
|---|---|
| `postgres-data` | Данные PostgreSQL |
| `product-images` | Изображения товаров |

---

## Хранение данных

Данные разделены по трём логическим базам данных PostgreSQL в одном контейнере:

| База данных | Сервис | Назначение |
|---|---|---|
| `shop_identity` | Shop.IdentityService | Пользователи, роли, refresh-токены |
| `shop_catalog` | Shop.CatalogService | Категории, товары, остатки, резервирования |
| `shop_orders` | Shop.OrderService | Корзины, заказы, история статусов |

Базы данных создаются автоматически скриптом `deploy/postgres/init-databases.sql` при первом запуске контейнера. Миграции EF Core применяются каждым сервисом самостоятельно при старте.

---

## Документация

Материалы курсового проекта находятся в каталоге [`docs/`](docs/):

| Каталог | Содержимое |
|---|---|
| `docs/coursework/` | Пояснительная записка, задание |
| `docs/architecture/` | Описание архитектуры системы |
| `docs/api/` | Документация REST API и gRPC-контрактов |
| `docs/deployment/` | Руководство по развёртыванию |
| `docs/ui/` | Описание пользовательского интерфейса |
| `docs/diagrams/` | Архитектурные и UML-схемы |
| `docs/audit/` | Финальный аудит и отчёты |
| `docs/screenshots/` | Скриншоты приложения |

---

## Безопасность

- **JWT** (HMAC-SHA256) для аутентификации и авторизации; одновременное использование в Identity, Catalog и Order сервисах.
- **Роли:** `Customer` (покупатель) и `Admin` (администратор).
- **Секреты** передаются через переменные окружения; в репозитории содержится только шаблон `.env.example`.
- **Реальные пароли, JWT-ключи и строки подключения к БД** в репозиторий не включены.

---

## Статус проекта

Завершённый учебный курсовой проект.
