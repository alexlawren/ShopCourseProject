# Cart API Specification (OrderService)

## Overview

The Cart API is provided by `Shop.OrderService` and allows authenticated users to manage their shopping cart.

### Key Architectural Rules
1. **Authentication Required**: All endpoints require a valid JWT bearer token issued by `Shop.IdentityService`. Anonymous requests return `401 Unauthorized`.
2. **Per-User Isolation**: The user ID (`UserId`) is always resolved from the JWT claim (`sub` / `NameIdentifier`). The client cannot specify a `userId` in routes, queries, or request bodies, ensuring absolute isolation between user carts.
3. **Lazy Cart Creation**: Calling `GET /api/cart` or `POST /api/cart/items` creates a new empty cart for the authenticated user if one does not already exist.
4. **Current Limitation (Change-set №5A)**:
   > **Note on Catalog Integration**: In Change-set №5A, `OrderService` does not communicate with `CatalogService`. Therefore, the Cart API currently stores only `ProductId` and `Quantity`. It does not validate product existence, active status, price, or stock levels. Cross-service validation and stock reservation will be introduced in subsequent change-sets via gRPC / asynchronous communication.

---

## Endpoints Summary

| Method | Route | Description | Auth Required | Success Status |
|---|---|---|---|---|
| `GET` | `/api/cart` | Get current user's cart (lazy creation) | Yes (Customer / Admin) | `200 OK` |
| `POST` | `/api/cart/items` | Add item to cart (increments quantity if already exists) | Yes (Customer / Admin) | `200 OK` |
| `PUT` | `/api/cart/items/{productId}` | Set absolute quantity of an item in cart | Yes (Customer / Admin) | `200 OK` |
| `DELETE` | `/api/cart/items/{productId}` | Remove item from cart (idempotent) | Yes (Customer / Admin) | `204 No Content` |
| `DELETE` | `/api/cart` | Clear all items from current user's cart (idempotent) | Yes (Customer / Admin) | `204 No Content` |

---

## Endpoints Detail

### 1. Get Current User Cart
- **Route**: `GET /api/cart`
- **Headers**: `Authorization: Bearer <jwt_token>`
- **Description**: Returns the shopping cart of the authenticated user. If no cart exists, an empty cart is created and returned.

#### Response: `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    {
      "productId": "8a2f1c84-1b32-47d9-bf3c-0e78c4a169b1",
      "quantity": 5
    }
  ],
  "totalQuantity": 5,
  "createdAtUtc": "2026-10-02T16:00:00Z",
  "updatedAtUtc": "2026-10-02T16:05:00Z"
}
```

---

### 2. Add Item to Cart
- **Route**: `POST /api/cart/items`
- **Headers**:
  - `Authorization: Bearer <jwt_token>`
  - `Content-Type: application/json`
- **Semantics**:
  - If `productId` is not yet in the cart, it is added with the specified `quantity`.
  - If `productId` already exists in the cart, the existing quantity is **incremented** by `quantity`.
  - The resulting quantity for any item cannot exceed `1000`. If it does, a `400 Bad Request` ProblemDetails response is returned.

#### Request Body
```json
{
  "productId": "8a2f1c84-1b32-47d9-bf3c-0e78c4a169b1",
  "quantity": 2
}
```

#### Validation Rules
- `productId`: required, non-empty Guid.
- `quantity`: integer, range 1 to 1000.

#### Response: `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    {
      "productId": "8a2f1c84-1b32-47d9-bf3c-0e78c4a169b1",
      "quantity": 2
    }
  ],
  "totalQuantity": 2,
  "createdAtUtc": "2026-10-02T16:00:00Z",
  "updatedAtUtc": "2026-10-02T16:00:00Z"
}
```

#### Error Responses
- `400 Bad Request`: Validation failure (empty `productId`, invalid `quantity`, or total item quantity > 1000).
- `401 Unauthorized`: Missing or invalid JWT.

---

### 3. Update Item Quantity
- **Route**: `PUT /api/cart/items/{productId}`
- **Headers**:
  - `Authorization: Bearer <jwt_token>`
  - `Content-Type: application/json`
- **Semantics**:
  - Sets the **absolute quantity** of the item to `request.Quantity`.
  - To delete an item, clients should use `DELETE /api/cart/items/{productId}` rather than sending quantity 0.

#### Request Body
```json
{
  "quantity": 7
}
```

#### Validation Rules
- `productId`: valid Guid in URL route.
- `quantity`: integer, range 1 to 1000.

#### Response: `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    {
      "productId": "8a2f1c84-1b32-47d9-bf3c-0e78c4a169b1",
      "quantity": 7
    }
  ],
  "totalQuantity": 7,
  "createdAtUtc": "2026-10-02T16:00:00Z",
  "updatedAtUtc": "2026-10-02T16:08:00Z"
}
```

#### Error Responses
- `400 Bad Request`: Invalid quantity (<= 0 or > 1000).
- `401 Unauthorized`: Missing or invalid JWT.
- `404 Not Found`: Item not found in user's cart.

---

### 4. Remove Item from Cart
- **Route**: `DELETE /api/cart/items/{productId}`
- **Headers**: `Authorization: Bearer <jwt_token>`
- **Semantics**:
  - Physically removes the specified `CartItem` from the user's cart.
  - Idempotent: returning `204 No Content` even if the item was not in the cart.
  - Does not delete the `Cart` entity itself.

#### Response: `204 No Content`

#### Error Responses
- `401 Unauthorized`: Missing or invalid JWT.

---

### 5. Clear Cart
- **Route**: `DELETE /api/cart`
- **Headers**: `Authorization: Bearer <jwt_token>`
- **Semantics**:
  - Deletes all `CartItem` records belonging to the current user's cart.
  - Idempotent: returning `204 No Content` on repeated calls.
  - The `Cart` entity remains preserved.

#### Response: `204 No Content`

#### Error Responses
- `401 Unauthorized`: Missing or invalid JWT.
