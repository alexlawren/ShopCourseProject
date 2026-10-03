# Shop.Web — Blazor WebAssembly Frontend Documentation

## 1. Overview & Architecture

`Shop.Web` is a client-side Single Page Application (SPA) built with **Blazor WebAssembly (.NET 9.0)**. It serves as the primary storefront user interface for the ShopCourseProject microservice system.

In accordance with **Architecture v1**:
- The browser application interacts **strictly and exclusively** through the **API Gateway** (`Shop.Gateway` running on `http://localhost:5210` in Development).
- Direct communication with downstream microservices (`Shop.IdentityService:5078`, `Shop.CatalogService:5058/5059`, `Shop.OrderService:5141`) is **strictly forbidden** in frontend code and configuration.
- The UI uses standard HTML5 semantic elements and the default Bootstrap CSS included with the .NET 9 WebAssembly template. No heavy third-party UI component libraries (MudBlazor, Radzen, etc.) are used, maintaining clean, understandable educational code.

> [!NOTE]
> **Change-set Scope Note**:
> - **Change-set №8A (Completed)** implemented: WASM Foundation, Gateway-only networking, Authentication (Register, Login, Logout, JWT State, Token Refresh, Session Storage), Public Product Catalog (Search, Category Filter, Price Filter, Stock Filter, Sorting, Pagination), Product Details, Image Serving via Gateway, Real-time Catalog Updates (SignalR CatalogHub), and Role-Aware Navigation.
> - **Change-set №8B.1 (Current)** implements: Shopping Cart UI, Add to Cart from Catalog and Product Details, Catalog Product Enrichment, Quantity Management, Cart Clearing, Decimal Preview Total, Idempotent Checkout with RequestId persistence/recovery, Customer Orders List with pagination, Order Details with historical snapshots and status timeline, Simulated Payment UI, Order Cancellation UI with browser confirmation, OrderHub SignalR Realtime integration, and Safe Mutation Token Refresh.
> - **Change-set №8B.2 (Upcoming)** will implement: Admin Catalog Management UI (categories, product CRUD, soft-delete, stock adjustment, image upload/delete) and Admin Orders UI (lifecycle controls, status progression, admin cancellation).

---

## 2. Gateway-Only Networking & URL Centralization

All network calls from `Shop.Web` route through `Shop.Gateway`:

```
┌────────────────────────────────────────────────────────┐
│             Browser (Shop.Web Client)                  │
│               http://localhost:5287                    │
└───────────────────────────┬────────────────────────────┘
                            │  All REST / Static / SignalR
                            ▼
┌────────────────────────────────────────────────────────┐
│             API Gateway (Shop.Gateway)                 │
│               http://localhost:5210                    │
└───────┬───────────────────┼────────────────────┬───────┘
        │ /api/auth/*       │ /api/catalog/*     │ /api/cart/*
        │                   │ /product-images/*  │ /api/orders/*
        │                   │ /hubs/catalog/*    │ /hubs/orders/*
        ▼                   ▼                    ▼
┌───────────────┐   ┌────────────────┐   ┌───────────────┐
│IdentityService│   │ CatalogService │   │ OrderService  │
│  (port 5078)  │   │  (port 5058)   │   │  (port 5141)  │
└───────────────┘   └────────────────┘   └───────────────┘
```

### Configuration & Base URL
The Gateway base URL is centrally configured in `wwwroot/appsettings.json`:
```json
{
  "Gateway": {
    "BaseUrl": "http://localhost:5210"
  }
}
```
If `Gateway:BaseUrl` is not provided (for example, in production with same-origin hosting), it falls back safely to `http://localhost:5210`. The `HttpClient` registered in DI has its `BaseAddress` configured to this Gateway origin. Components and services never hardcode `http://localhost:5210`.

---

## 3. Authentication, Token Management & Safe Mutation Refresh

### Browser Storage Model
`Shop.Web` implements token storage via the `ITokenStorage` abstraction backed by browser `sessionStorage` (`SessionStorageTokenStorage`) through minimal JavaScript interop (`sessionStorage.getItem`, `setItem`, `removeItem`).
- **Why `sessionStorage`?**: Survives browser page reloads (F5) within the active browser tab, but automatically clears upon tab closure, significantly reducing exposure compared to persistent `localStorage`.
- **Security boundary caveat**: Browser token storage in SPA is an educational simplification. In enterprise production systems, a Backend-For-Frontend (BFF) pattern with `HttpOnly`, `SameSite=Strict` secure cookies is recommended.
- **Client-Side Boundaries**: Client-side JWT parsing is used **exclusively for UI presentation** (e.g., displaying the user's email and role-specific badges). The backend microservices remain the sole authoritative security validators for signatures, lifetimes, and permissions. Frontend code never contains JWT signing keys or database credentials.

### Authentication State Provider
`CustomAuthenticationStateProvider` inherits from `AuthenticationStateProvider`:
- Reads the access token from `ITokenStorage`.
- Uses `JwtClaimsParser` to decode Base64Url payload claims (`sub`, `email`, `role`).
- Constructs a `ClaimsPrincipal` with `ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role)`.
- Updates UI state and notifies subscribers via `NotifyAuthenticationStateChanged` during login, logout, and token refresh.

### Safe Mutation Refresh Strategy
In Change-set №8B.1, protected operations include mutating HTTP requests (`POST`, `PUT`, `DELETE`). Blindly retrying non-idempotent mutations after receiving a `401 Unauthorized` is unsafe. `Shop.Web` applies a proactive refresh strategy:
1. **Proactive Expiration Verification**: Before any non-auth request is dispatched, `AuthHeaderHandler` inspects the access token's `exp` claim via `JwtClaimsParser.IsExpiredOrExpiringSoon(accessToken, clockSkew: 30s)`.
2. **Pre-flight Token Refresh**: If the token is expired or within 30 seconds of expiry, `IAuthService.RefreshAsync()` is invoked *before* the mutation request leaves the client.
3. **Concurrency Control**: `AuthService` synchronizes refresh requests using `SemaphoreSlim(1, 1)` with double-checked locking, ensuring multiple parallel requests share a single refresh call.
4. **Targeted GET Retry Fallback**: If a `401 Unauthorized` is still encountered, the token is refreshed and retried **strictly for GET requests**. Mutation requests (POST, PUT, DELETE) are never automatically replayed upon 401, preserving backend safety.
5. **Session Teardown on Failure**: When a refresh attempt fails (e.g. refresh token expired or revoked), all local tokens are purged, `OrderRealtimeService` is stopped, and the user transitions cleanly to the anonymous state.

---

## 4. Public Product Catalog & Query Model

### Features
The `/catalog` page provides a responsive shopping catalog with:
- **Search**: Case-insensitive substring matching against product name and description via backend PostgreSQL ILIKE.
- **Category Filter**: Dynamically loaded from `/api/catalog/categories` through Gateway.
- **Price Range Filter**: `minPrice` and `maxPrice` decimal filters (formatted with invariant culture).
- **Stock Availability Filter**: Toggle for in-stock items (`inStock=true`).
- **Sorting**: Options for newest, price ascending, price descending, and name alphabetical.
- **Server Pagination**: Uses backend `PagedResult<T>` metadata (`page`, `pageSize`, `totalItems`, `totalPages`) with Previous/Next controls.
- **Add to Cart**: Authenticated customers can add products directly from the catalog listing or product details page with quantity controls (default 1). Anonymous visitors see an invitation link to log in.

### Product Images via Gateway
Product images are returned from the CatalogService with relative paths (e.g. `/product-images/guid.png`). The frontend uses `GatewayImageUrlBuilder` to construct absolute URLs directed through the Gateway (`http://localhost:5210/product-images/guid.png`). If a product has no image, a clean inline SVG/CSS placeholder is rendered without external stock asset dependencies.

---

## 5. Catalog Realtime Updates (SignalR)

### Gateway Hub Connection
The frontend connects to the public `CatalogHub` via the API Gateway:
```
ws://localhost:5210/hubs/catalog
```
`CatalogRealtimeService` wraps `HubConnection` with automatic reconnection (`.WithAutomaticReconnect()`).

### Supported Events
1. **`StockChangedEvent`** (`ProductId`, `NewStockQuantity`, `TimestampUtc`):
   - In `/catalog`: If the modified product is present in the current view, its stock count and availability badge ("В наличии" vs "Нет в наличии") are updated in-place without page reload.
   - In `/products/{id}`: The product stock is updated dynamically.
2. **`ProductChangedEvent`** (`ProductId`, `Action`, `TimestampUtc`):
   - In `/catalog`: Triggers a background refetch of the current catalog page. If a product was updated, new details appear; if a product was soft-deleted (`IsActive = false`), it automatically disappears from the public listing.
   - In `/products/{id}`: Triggers a reload of product details; if deleted, displays the "Товар не найден" state.

### Lifecycle Management
Components implement `IDisposable` / `IAsyncDisposable` to properly unsubscribe from realtime events when navigating away, preventing memory leaks and duplicate handler invocations.

---

## 6. Shopping Cart UI & Catalog Product Enrichment

### Routing & Protection
The `/cart` page is accessible only to authenticated users (wrapped in `<AuthorizeView>` with redirection prompt).

### Catalog Product Enrichment
The backend `OrderService` stores only `ProductId` and `Quantity` in `shop_orders.CartItems`, maintaining service decoupling.
`Cart.razor` enriches cart items into rich `CartItemViewModel` objects:
- Gathers unique `ProductId` values from `CartDto`.
- Queries `ICatalogApiClient.GetProductByIdAsync` in parallel using `Task.WhenAll`.
- Populates product name, current unit price, stock quantity, active status, and image URL.
- **Unavailable / Deactivated Items**: If a product has been deleted or deactivated in the catalog, it is flagged as unavailable (`IsAvailable = false`). The UI displays an alert and allows removing the item from the cart, while blocking checkout until resolved.

### Preview Total Semantics
- Cart total amount is calculated on the client using decimal precision (`CartTotalHelper.CalculateTotal`).
- Clear UI disclaimer is shown: the client-side total is an estimate based on current catalog prices. Authoritative pricing is locked by the backend during checkout via the atomic gRPC stock reservation snapshot.

### Quantity & Cart Controls
- **Quantity Stepper / Input**: Absolute quantity adjustments (1..1000) send `PUT /api/cart/items/{productId}`.
- **Item Removal**: Remove button sends `DELETE /api/cart/items/{productId}`.
- **Clear Cart**: "Очистить корзину" button triggers a native browser confirmation dialog before calling `DELETE /api/cart`.

---

## 7. Checkout UI & Idempotency RequestId Lifecycle

### Orchestration
Clicking "Оформить заказ" initiates `POST /api/orders` through `IOrderApiClient.CheckoutAsync(requestId)`.

### RequestId Rules & Failure Recovery
Idempotency is preserved across retries without risking duplicate orders or double stock decrements:
1. **Pending RequestId Generation**: A unique `Guid` is generated and persisted in `sessionStorage` under `pending_checkout_request_id` via `ICheckoutRequestIdStorage`.
2. **Uncertain Failure Preservation**: If checkout fails due to a network drop, timeout, HTTP 503 (`CATALOG_UNAVAILABLE`), or HTTP 502 (`DOWNSTREAM_ERROR`), the pending `RequestId` is **preserved**. Clicking "Повторить заказ" reuses the exact same `RequestId`, activating the backend idempotency fast-path.
3. **Definitive Success Clearance**: Upon receiving HTTP 201 Created (new order) or HTTP 200 OK (idempotent existing order), the pending `RequestId` is removed from `sessionStorage`, and the user is redirected to the order details page.
4. **Deterministic Validation Failure Clearance**: When backend rejects checkout before order creation (e.g. `EMPTY_CART`, `CART_CHANGED`, `OUT_OF_STOCK`, `PRODUCT_NOT_FOUND`, `PRODUCT_INACTIVE`, `CATEGORY_INACTIVE`), the pending `RequestId` is cleared so the customer can adjust their cart and start a fresh checkout.
5. **Double Submit Prevention**: The checkout button is disabled with a loading indicator while the operation is pending.

---

## 8. Customer Orders UI, Details & Historical Snapshots

### Orders List (`/orders`)
- Displays paginated customer orders sorted newest first (`GET /api/orders?page={p}&pageSize=10`).
- Columns: Order ID (shortened GUID), Date, Status badge, Payment status badge, Total amount, and "Подробнее" link.
- Server-side pagination with Previous/Next controls.

### Order Details (`/orders/{id}`)
- Displays full order metadata: full GUID, creation timestamp, status, and payment status.
- **Historical Snapshots**: Line items (`ProductName`, `UnitPrice`, `Quantity`, `LineTotal`) are rendered strictly from the backend `OrderItem` snapshot taken at checkout time, never overwritten with current catalog prices or renamed products.
- **Status History Timeline**: Chronological audit trail of order states (`OrderStatusHistory`) with UTC timestamps.
- **User Ownership Isolation**: If a customer attempts to access another user's order ID, the backend returns 404 Not Found, and the UI displays "Заказ не найден".

---

## 9. Simulated Payment & Order Cancellation UI

### Simulated Payment
- Available on `/orders/{id}` when `PaymentStatus == Pending` and order is not `Cancelled`.
- Calls `POST /api/orders/{id}/pay` via `IOrderApiClient.PayAsync`.
- Shows clear educational notice: *"Учебная симуляция оплаты — реальные денежные средства не списываются."*
- Disables button during request; updates status to `Confirmed` and payment to `Paid` upon completion.

### Order Cancellation
- Cancellation button is visible only when order status is cancellable (`Created`, `Confirmed`, or `Processing`).
- Hidden for terminal or advanced states (`Shipped`, `Completed`, `Cancelled`).
- Prompts for explicit user confirmation via native browser dialog.
- Calls `POST /api/orders/{id}/cancel` via `IOrderApiClient.CancelAsync`.
- Handles `ORDER_CANCELLATION_IN_PROGRESS`: displays a friendly retry message without exposing internal state.

---

## 10. OrderHub SignalR Realtime Updates

### Connection & Authentication
- Connects through API Gateway: `<GatewayBaseUrl>/hubs/orders`.
- Scoped service `OrderRealtimeService` configured with `.WithAutomaticReconnect()`.
- Dynamic `AccessTokenProvider`: passes the latest JWT access token from `ITokenStorage` on every connection and reconnection attempt.
- Lifecycle: initialized when an authenticated user opens orders or cart, cleanly stopped during user logout.

### Supported Events
1. **`OrderCreated`**: On `/orders`, appends new orders or triggers list reload.
2. **`OrderStatusChanged`**: Idempotently updates the order status in the list or on the details page, and reloads order details to refresh the history timeline.
3. **`PaymentStatusChanged`**: Idempotently updates the order payment status.

---

## 11. Role-Aware Navigation

The top navigation bar (`NavMenu.razor`) uses `AuthorizeView` to adjust navigation:
- **Anonymous**: Shows links to **Каталог**, **Вход** (`/login`), and **Регистрация** (`/register`).
- **Authenticated (Customer)**: Shows links to **Каталог**, **Корзина** (`/cart`), **Мои заказы** (`/orders`), user email, "Покупатель" badge, and **Выход** (Logout).
- **Authenticated (Admin)**: Shows links to **Каталог**, **Корзина**, **Мои заказы**, user email, "Администратор" badge, and **Выход** (Logout).
- *Admin management pages (Catalog management and Orders management) are intentionally deferred to Change-set №8B.2.*

