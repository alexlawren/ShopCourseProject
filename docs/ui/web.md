# Shop.Web — Blazor WebAssembly Frontend Documentation

## 1. Overview & Architecture

`Shop.Web` is a client-side Single Page Application (SPA) built with **Blazor WebAssembly (.NET 9.0)**. It serves as the primary storefront user interface for the ShopCourseProject microservice system.

In accordance with **Architecture v1**:
- The browser application interacts **strictly and exclusively** through the **API Gateway** (`Shop.Gateway` running on `http://localhost:5210` in Development).
- Direct communication with downstream microservices (`Shop.IdentityService:5078`, `Shop.CatalogService:5058/5059`, `Shop.OrderService:5141`) is **strictly forbidden** in frontend code and configuration.
- The UI uses standard HTML5 semantic elements and the default Bootstrap CSS included with the .NET 9 WebAssembly template. No heavy third-party UI component libraries (MudBlazor, Radzen, etc.) are used, maintaining clean, understandable educational code.

> [!NOTE]
> **Change-set Scope Note**:
> - **Change-set №8A (Current)** implements: WASM Foundation, Gateway-only networking, Authentication (Register, Login, Logout, JWT State, Token Refresh, Session Storage), Public Product Catalog (Search, Category Filter, Price Filter, Stock Filter, Sorting, Pagination), Product Details, Image Serving via Gateway, Real-time Catalog Updates (SignalR CatalogHub), and Role-Aware Navigation.
> - **Change-set №8B (Upcoming)** will implement: Shopping Cart UI, Checkout UI, Customer Orders UI, Payment Simulation / Order Cancellation UI, Admin Catalog Management UI, Admin Orders UI, and OrderHub Realtime Integration.

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

## 3. Authentication & Token Management

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

### Refresh Token Flow & Concurrency
When a protected API request encounters a `401 Unauthorized`:
1. `AuthHeaderHandler` (a `DelegatingHandler` scoped to the Gateway `HttpClient`) intercepts the response.
2. If the request is not an authentication endpoint, it calls `IAuthService.RefreshAsync()`.
3. `AuthService` synchronizes refresh requests using `SemaphoreSlim(1, 1)` to prevent race conditions when multiple parallel HTTP requests fail simultaneously.
4. On refresh success: new tokens are persisted to `sessionStorage`, `CustomAuthenticationStateProvider` is notified, and the failed GET request is retried once with the fresh token.
5. On refresh failure (token revoked/expired): tokens are cleared from storage, and the UI transitions to the anonymous state.

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

## 6. Role-Aware Navigation

The top navigation bar (`NavMenu.razor`) uses `AuthorizeView` to adjust navigation:
- **Anonymous**: Shows links to **Каталог**, **Вход** (`/login`), and **Регистрация** (`/register`).
- **Authenticated (Customer)**: Shows user email, a "Покупатель" badge, and **Выход** (Logout).
- **Authenticated (Admin)**: Shows user email, an "Администратор" badge, and **Выход** (Logout).
- *Admin management pages, Shopping Cart, and Orders UI are intentionally deferred to Change-set №8B.*
