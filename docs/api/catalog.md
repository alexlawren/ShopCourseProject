# Catalog API

Base path: `/api/catalog`

Public read-only API for browsing categories and products in the shop catalog.
All endpoints return standard ASP.NET Core `ProblemDetails` on error.
Deactivated products (`IsActive = false`) and products assigned to deactivated categories are hidden from all public responses.

---

## GET /api/catalog/categories

Returns all active categories sorted alphabetically by name.

**Authentication**: None (Public)

**Success**: `200 OK`
```json
[
  {
    "id": "9d16d27a-90bb-4bd3-ae8f-c846c9136d0a",
    "name": "Laptops",
    "slug": "laptops"
  },
  {
    "id": "f5b21183-4a15-46f9-b883-74b655da527a",
    "name": "Smartphones",
    "slug": "smartphones"
  }
]
```

---

## GET /api/catalog/products

Returns a paginated list of active products with optional search, filtering, and sorting.

**Authentication**: None (Public)

### Query Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `search` | string | `null` | Case-insensitive substring search matching `Name` or `Description` via PostgreSQL `ILIKE`. |
| `categoryId` | UUID | `null` | Filters products by Category ID. |
| `minPrice` | decimal | `null` | Lower price bound (inclusive). Must be `>= 0`. |
| `maxPrice` | decimal | `null` | Upper price bound (inclusive). Must be `>= 0` and `>= minPrice`. |
| `inStock` | boolean | `null` | `true`: returns only products with `StockQuantity > 0`.<br>`false`: returns only products with `StockQuantity == 0`.<br>omitted/`null`: does not filter by stock. |
| `sort` | string | `newest` | Sort order. Supported values:<br>- `newest`: newest first (`CreatedAtUtc DESC`)<br>- `priceAsc`: price ascending (`Price ASC`)<br>- `priceDesc`: price descending (`Price DESC`)<br>- `nameAsc`: name alphabetical (`Name ASC`)<br>- `nameDesc`: name reverse alphabetical (`Name DESC`) |
| `page` | integer | `1` | 1-based page index. Must be `>= 1`. |
| `pageSize` | integer | `20` | Items per page. Must be between `1` and `100`. |

### Example Request

```http
GET /api/catalog/products?search=thinkpad&categoryId=9d16d27a-90bb-4bd3-ae8f-c846c9136d0a&minPrice=500&maxPrice=2500&inStock=true&sort=priceAsc&page=1&pageSize=20
```

### Success: `200 OK`

```json
{
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Lenovo ThinkPad X1 Carbon",
      "price": 1500.00,
      "stockQuantity": 10,
      "imagePath": "/images/thinkpad.png",
      "categoryId": "9d16d27a-90bb-4bd3-ae8f-c846c9136d0a",
      "categoryName": "Laptops"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

### Errors

| Status | Reason | Schema |
|---|---|---|
| 400 Bad Request | Invalid parameter values (`page < 1`, `pageSize < 1`, `pageSize > 100`, `minPrice < 0`, `maxPrice < 0`, `minPrice > maxPrice`, unsupported `sort`) | `ValidationProblemDetails` |

**Example 400 Bad Request Response**:
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The product query parameters are invalid.",
  "errors": {
    "MinPrice": [
      "MinPrice cannot be greater than MaxPrice."
    ]
  }
}
```

---

## GET /api/catalog/products/{id}

Returns full product details by its unique identifier.

**Authentication**: None (Public)

**Success**: `200 OK`
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Lenovo ThinkPad X1 Carbon",
  "description": "Premium lightweight business laptop with OLED display.",
  "price": 1500.00,
  "stockQuantity": 10,
  "imagePath": "/images/thinkpad.png",
  "category": {
    "id": "9d16d27a-90bb-4bd3-ae8f-c846c9136d0a",
    "name": "Laptops",
    "slug": "laptops"
  },
  "createdAtUtc": "2026-09-30T10:00:00Z"
}
```

### Errors

| Status | Reason | Schema |
|---|---|---|
| 404 Not Found | Product does not exist, product is inactive (`IsActive = false`), or product belongs to an inactive category | `ProblemDetails` |

**Example 404 Not Found Response**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Product Not Found",
  "status": 404,
  "detail": "Product with ID '11111111-1111-1111-1111-111111111111' was not found or is inactive."
}
```
