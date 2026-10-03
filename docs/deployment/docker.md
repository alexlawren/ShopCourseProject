# Руководство по развертыванию в Docker / Docker Compose

Данный документ описывает процедуру контейнеризации и оркестрации системы **ShopCourseProject** в рамках утвержденной архитектуры **Architecture v1** (Change-set №9).

---

## 1. Архитектура развертывания

Система развертывается как единый комплекс взаимосвязанных контейнеров в изолированной мостовой сети Docker (`shop-network`).

```
                +------------------------------------------------+
                |             Пользователь / Браузер             |
                +------------------------------------------------+
                                        |
                            HTTP / WS (Port 8080)
                                        v
+--------------------------------------------------------------------------------+
| DOCKER BRIDGE NETWORK: shop-network                                            |
|                                                                                |
|  +--------------------------------------------------------------------------+  |
|  |                          gateway (Shop.Gateway)                          |  |
|  |  - Порт на хосте: 8080:8080                                              |  |
|  |  - Статический хостинг: Blazor WebAssembly (/ -> /app/wwwroot)           |  |
|  |  - Reverse Proxy (YARP): /api/*, /hubs/*, /product-images/*               |  |
|  +--------------------------------------------------------------------------+  |
|         |                     |                    |                |          |
|    HTTP |                HTTP |               HTTP |          HTTP  |          |
|         v                     v                    v                |          |
|  +--------------+    +------------------+    +---------------+      |          |
|  |   identity   |    |     catalog      |    |     order     |      |          |
|  |   :8080      |    |   REST: 8080     |    |     :8080     |      |          |
|  +--------------+    |   gRPC: 8081     |<---+---------------+      |          |
|         |            +------------------+    gRPC (Direct internal) |          |
|         |                     |                      |              |          |
|         +----------+----------+----------------------+              |          |
|                    | TCP 5432 (PostgreSQL)                          |          |
|                    v                                                v          |
|             +--------------+                               +-----------------+ |
|             |   postgres   |                               | Volume:         | |
|             | postgres:17  |                               | product-images  | |
|             +--------------+                               +-----------------+ |
|                    |                                                           |
|             +--------------+                                                   |
|             | Volume:      |                                                   |
|             | postgres-data|                                                   |
|             +--------------+                                                   |
+--------------------------------------------------------------------------------+
```

### Ключевые архитектурные решения:
1. **Single External Origin (Единая точка входа)**:
   - Внешний трафик поступает исключительно через контейнер `gateway` на порт `8080`.
   - Внутренние микросервисы (`identity-service`, `catalog-service`, `order-service`) и СУБД `postgres` **не пробрасывают порты на хост-машину**. Это исключает коллизии с локальными сервисами разработчика (например, локальной PostgreSQL на порту 5432).
   - `Shop.Gateway` совмещает функции обратного прокси YARP и веб-сервера статических файлов Blazor WASM (`Shop.Web`), что исключает необходимость в дополнительном Nginx-контейнере и решает проблему Same-Origin для SPA, REST API и WebSockets.
2. **Прямое межсервисное gRPC-взаимодействие**:
   - `order-service` взаимодействует с `catalog-service` по gRPC напрямую через внутреннюю сеть Docker: `http://catalog-service:8081` (h2c, cleartext), минуя Gateway.
3. **Хранилище постоянных данных (Persistent Volumes)**:
   - `postgres-data` — том для файлов баз данных PostgreSQL.
   - `product-images` — том для загруженных изображений товаров (`/data/product-images`).

---

## 2. Предварительные требования

- **Docker Engine** 24.0+ / **Docker Desktop** (на Windows: с бэкендом WSL2).
- **Docker Compose** v2.20+.
- Свободный порт `8080` на хосте (настраивается переменной `GATEWAY_PORT`).

---

## 3. Настройка конфигурации (`.env`)

Перед первым запуском необходимо создать файл конфигурации окружения `.env` в корне репозитория на основе шаблона [`.env.example`](file:///d:/учеба/4%20курс/1%20сем/courswork_psp/.env.example):

```bash
cp .env.example .env
```

### Основные параметры `.env`:

| Параметр | Описание | Значение по умолчанию |
| :--- | :--- | :--- |
| `POSTGRES_USER` | Пользователь PostgreSQL | `shop` |
| `POSTGRES_PASSWORD` | Пароль пользователя PostgreSQL | *Обязательный (задаётся в `.env`)* |
| `JWT_KEY` | Симметричный ключ подписи JWT (>= 32 байт) | *Обязательный (задаётся в `.env`)* |
| `BOOTSTRAP_ADMIN_EMAIL` | Email начального администратора | *(Опционально, по умолчанию не задан)* |
| `BOOTSTRAP_ADMIN_PASSWORD` | Пароль начального администратора | *(Опционально, задаётся в `.env`)* |
| `GATEWAY_PORT` | Внешний порт шлюза на хост-системе | `8080` |

> [!IMPORTANT]
> Файл `.env` добавлен в `.dockerignore` и `.gitignore` и не должен попадать в систему контроля версий.

---

## 4. Сборка и запуск

### 4.1. Сборка Docker-образов
```bash
docker compose build
```
Используются многоэтапные Dockerfile (`deploy/docker/Dockerfile.*`) на базе легковесных образов `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` и `mcr.microsoft.com/dotnet/sdk:9.0-alpine`.

### 4.2. Фоновый запуск сервисов
```bash
docker compose up -d
```
Docker Compose запустит сервисы в строгом порядке с учетом зависимостей:
1. `postgres` (с ожиданием прохождения `healthcheck` pg_isready);
2. `identity-service`, `catalog-service`, `order-service` (автоматически применяют миграции EF Core и сидят пользователя-администратора);
3. `gateway` (запускается после готовности сервисов и открывает порт `8080`).

### 4.3. Проверка статуса контейнеров
```bash
docker compose ps
```
Все 5 контейнеров должны находиться в статусе `Up` / `healthy`.

### 4.4. Просмотр журналов
```bash
# Журналы всех сервисов
docker compose logs -f

# Журнал конкретного сервиса
docker compose logs -f gateway
docker compose logs -f catalog-service
docker compose logs -f order-service
docker compose logs -f identity-service
```

### 4.5. Остановка и перезапуск
```bash
# Обычная остановка контейнеров с сохранением данных
docker compose stop

# Возобновление работы
docker compose start

# Перезапуск всех сервисов
docker compose restart

# Остановка с удалением контейнеров (данные в томах СОХРАНЯЮТСЯ)
docker compose down

# Полная очистка с удалением томов данных
docker compose down -v
```

---

## 5. Проверка работоспособности (Health & Smoke Test)

1. **Проверка работоспособности шлюза**:
   ```bash
   curl http://localhost:8080/health
   # Ответ: {"status":"Healthy","service":"Gateway"}
   ```

2. **Доступ к веб-интерфейсу Blazor SPA**:
   Открыть в браузере: `http://localhost:8080/`

3. **Вход под администратором (при настройке BootstrapAdmin в .env)**:
   - Email: значение `BOOTSTRAP_ADMIN_EMAIL` из `.env`
   - Пароль: значение `BOOTSTRAP_ADMIN_PASSWORD` из `.env`

4. **Запуск комплексного дымового теста**:
   Для автоматической валидации всех 11 контрольных точек развертывания (Health, Static WASM, Auth, RBAC, Catalog CRUD, Images, gRPC Cart/Checkout, Lifecycle, Cancellation, SignalR) предусмотрен скрипт:
   ```bash
   dotnet run --project scratch/DockerSmokeRunner/DockerSmokeRunner.csproj
   ```

---

## 6. Отличия режимов Development и Docker Production

| Характеристика | Local Development (`dotnet run`) | Docker Production Compose |
| :--- | :--- | :--- |
| **Порт Gateway** | `http://localhost:5210` | `http://localhost:8080` (через `.env`) |
| **Хостинг Blazor** | Standalone Kestrel dev-server (5287) | Gateway embedded static hosting (8080) |
| **Origin запросов** | Разные (5287 -> 5210 с CORS) | Same-Origin (8080 -> 8080) |
| **Базы данных** | Хостовая PostgreSQL 5432 (shop_*) | Изолированная контейнерная PostgreSQL |
| **Миграции EF Core** | `dotnet ef database update` | Автоматически на старте контейнеров |
| **Каталог картинок** | Локальная директория `data/` | Именованный том `product-images` |
| **Межсервисный gRPC** | `http://localhost:5059` | `http://catalog-service:8081` |
