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
> - **Change-set №8B.1 (Completed)** implemented: Shopping Cart UI, Add to Cart from Catalog and Product Details, Catalog Product Enrichment, Quantity Management, Cart Clearing, Decimal Preview Total, Idempotent Checkout with RequestId persistence/recovery, Customer Orders List with pagination, Order Details with historical snapshots and status timeline, Simulated Payment UI, Order Cancellation UI with browser confirmation, OrderHub SignalR Realtime integration, and Safe Mutation Token Refresh.
> - **Change-set №8B.2 (Completed)** implements: Admin Commerce UI, including Admin Dashboard, Inactive/Active Category Management (Create, Edit, Soft-delete, Reactivate), Product Management (Search, Category/Active filters, Create, Edit, Soft-delete, Reactivate), Stock Management (Absolute quantity PATCH), Product Image Administration (Upload via InputFile, Static Serving through Gateway, Image Deletion), Admin Orders List with customer UserId (no Identity DB lookup), Status and Payment Status filtering, Server-side pagination, Order Details with historical snapshots and status history audit timeline, Order Lifecycle Controls (forward progression state machine), Admin Cancellation with Stock Return, and Admin Realtime Updates (CatalogHub and OrderHub `admins` group).

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

## 11. Role-Aware Navigation & Route Authorization

### Navigation Bar (`NavMenu.razor`)
The top navigation bar uses standard Blazor `AuthorizeView` components to dynamically adjust available links based on the user's authentication and role state:
- **Anonymous**: Shows links to **Каталог**, **Вход** (`/login`), and **Регистрация** (`/register`).
- **Authenticated (Customer)**: Shows links to **Каталог**, **Корзина** (`/cart`), **Мои заказы** (`/orders`), user email, "Покупатель" badge, and **Выход** (Logout).
- **Authenticated (Admin)**: In addition to customer storefront links, renders an **Администрирование** section containing:
  - **Панель** (`/admin`)
  - **Каталог (Admin)** (`/admin/catalog`)
  - **Заказы (Admin)** (`/admin/orders`)
  - "Администратор" badge and **Выход** (Logout).

Customers and anonymous visitors never see admin navigation links.

### Route Authorization (`App.razor`)
All admin pages (`/admin`, `/admin/catalog`, `/admin/orders`, `/admin/orders/{id}`) are guarded with `@attribute [Authorize(Roles = "Admin")]`.
The routing pipeline in `App.razor` evaluates permissions via `AuthorizeRouteView`:
- **Unauthenticated / Anonymous Access**: Displays a friendly login invitation prompting authentication.
- **Customer Access to Admin Routes**: Displays a 403 Forbidden alert (*"Доступ ограничен. Данный раздел доступен только администраторам."*) with a button to return to the public catalog.
- **Backend Authority**: Frontend route guarding is strictly for user experience; backend microservices remain the authoritative security boundary, returning HTTP 401 for unauthenticated calls and HTTP 403 Forbidden for customer calls to admin endpoints.

---

## 12. Admin Dashboard (`/admin`)

The `/admin` route provides a clean, focused administrative dashboard:
- Navigation cards for **Управление каталогом** (`/admin/catalog`) and **Управление заказами** (`/admin/orders`).
- Displays role information and quick access shortcuts.
- Avoids arbitrary synthetic analytics or graphs not backed by backend APIs.

---

## 13. Admin Catalog Management UI (`/admin/catalog`)

### Category Management
- **Listing**: Displays all categories, including active and inactive (soft-deleted) entities via dedicated backend admin endpoint `GET /api/catalog/admin/categories`.
- **Creation**: Modal/card form accepting `Name` and `Slug`.
- **Slug Validation**: Client-side validation enforcing the backend regex contract `^[a-z0-9]+(?:-[a-z0-9]+)*$` via `AdminCatalogValidatorHelper.IsValidSlug`, backed by authoritative backend uniqueness and validation checks.
- **Editing**: Allows modifying `Name`, `Slug`, and `IsActive` toggle via `PUT /api/catalog/categories/{id}`.
- **Soft Deletion & Reactivation**:
  - Deactivation sends `DELETE /api/catalog/categories/{id}` (soft-delete, `IsActive = false`).
  - Reactivation sends `PUT /api/catalog/categories/{id}` with `IsActive = true`.
  - Inactive categories hide their associated products from public catalog queries, but remain fully visible and manageable in the Admin UI.

### Product Management
- **Listing & Filters**: Filter by search text, category, and active status (`all`, `active`, `inactive`). Supports server-side pagination through `GET /api/catalog/admin/products`.
- **Creation**: Form accepting `CategoryId`, `Name`, `Description`, `Price` (>= 0), and initial `StockQuantity` (>= 0) via `POST /api/catalog/products`. Product images are not included in creation and are managed as a separate operation.
- **Editing**: Modifies `CategoryId`, `Name`, `Description`, `Price`, and `IsActive` via `PUT /api/catalog/products/{id}`. Stock and image are excluded from product update requests to maintain domain separation.
- **Soft Deletion & Reactivation**:
  - Soft-delete sends `DELETE /api/catalog/products/{id}` (`IsActive = false`).
  - Reactivation sends `PUT /api/catalog/products/{id}` with `IsActive = true`.
  - Deactivated products disappear from the public catalog, but remain manageable in the admin view.

### Stock Management
- Dedicated stock adjustment control for setting absolute inventory levels (`StockQuantity >= 0`).
- Dispatches `PATCH /api/catalog/products/{id}/stock` with payload `{ "newStockQuantity": Q }`.
- Never simulates stock modification via product update PUT requests.

### Product Image Administration
- **Image Upload**:
  - Utilizes standard Blazor `InputFile` component.
  - Client-side validation checks allowed MIME types (`image/jpeg`, `image/png`, `image/webp`) and maximum file size (5 MiB) via `AdminCatalogValidatorHelper.ValidateImage`.
  - Streamed via `IBrowserFile.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024)` into `MultipartFormDataContent` with form field name `file`.
  - Backend magic-byte inspection remains authoritative security validator.
  - Sent via `POST /api/catalog/products/{id}/image`.
- **Gateway Image Serving**:
  - Uploaded images are referenced via relative URLs (`/product-images/<guid>.<ext>`) and resolved through `Shop.Gateway` (`http://localhost:5210/product-images/...`).
- **Image Deletion**:
  - Dispatches `DELETE /api/catalog/products/{id}/image`.
  - Reverts product image to the default placeholder. Idempotent on repeated calls.

---

## 14. Admin Orders Management UI (`/admin/orders`)

### Orders Listing (`/admin/orders`)
- **Backend-Driven Filters**: Filters by order status (`Created`, `Confirmed`, `Processing`, `Shipped`, `Completed`, `Cancelled`) and payment status (`Pending`, `Paid`, `Cancelled`).
- **Server Pagination**: Server-side pagination with page size controls.
- **Displayed Columns**: Order ID (GUID), Customer `UserId` (GUID), Date (UTC), Order Status badge, Payment Status badge, Total Amount, and Actions link.
- **Privacy & Service Decoupling Boundary**: Displays only `UserId` stored in `shop_orders`. No direct calls or database queries are made to `IdentityService` to fetch user email or profile data.

### Admin Order Details (`/admin/orders/{id}`)
- Displays full order metadata, including Order ID, Customer `UserId`, creation and update timestamps, current status, payment status, and total amount.
- **Historical Snapshots**: Renders order line items from immutable snapshots (`ProductName`, `UnitPrice`, `Quantity`, `LineTotal`).
- **Status Audit History**: Chronological timeline of `OrderStatusHistory` entries.

### Forward Lifecycle Controls
- Guided status transition progression managed via `AdminOrderUiHelper`:
  - `Created` → `Confirmed`
  - `Confirmed` → `Processing`
  - `Processing` → `Shipped`
  - `Shipped` → `Completed`
- Dispatches `PATCH /api/admin/orders/{id}/status` with target status string.
- Invalid or backwards transitions are blocked in UI and rejected with HTTP 409 Conflict by backend authority.

### Admin Cancellation with Stock Return
- Cancellation button displayed only for cancellable statuses (`Created`, `Confirmed`, `Processing`).
- Hidden for advanced/terminal statuses (`Shipped`, `Completed`, `Cancelled`).
- Dispatches dedicated endpoint `POST /api/admin/orders/{id}/cancel` (never PATCH status to Cancelled).
- Triggers durable 3-phase cancellation orchestration returning committed inventory via Catalog gRPC.
- Handles `ORDER_CANCELLATION_IN_PROGRESS` with retry guidance without exposing internal state.

---

## 15. Realtime Updates in Admin UI

### CatalogRealtimeService in Admin Catalog
- Subscribes to `OnStockChanged`: updates product stock quantity dynamically if customer checkouts occur concurrently.
- Subscribes to `OnProductChanged`: triggers background refetch to reflect changes without full page reload.

### OrderRealtimeService in Admin Orders
- Automatically joins the SignalR `admins` group upon connecting with an Admin JWT.
- Subscribes to:
  - `OrderCreated`: Refetches current orders page so newly placed customer orders appear immediately.
  - `OrderStatusChanged`: Refetches or updates order status and refreshes details timeline.
  - `PaymentStatusChanged`: Refetches or updates payment status badges.

---

## 16. Safe Mutation Refresh in Admin UI

Administrative operations involve sensitive mutations (`POST`, `PUT`, `DELETE`, `PATCH`).
`AuthHeaderHandler` proactively checks token expiry before sending mutations:
- If expired or expiring within 30 seconds, performs proactive token refresh before dispatching the mutation.
- Prevents replaying multipart image upload streams or state transition requests upon 401.


