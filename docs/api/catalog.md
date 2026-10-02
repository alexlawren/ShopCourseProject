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


---

# Admin API

All write endpoints in the catalog require authentication with a valid JWT token issued by IdentityService and carrying the `Admin` role (`Role = "Admin"`).
Unauthorized requests return `401 Unauthorized` (missing/invalid token) or `403 Forbidden` (valid token without `Admin` role).

Delete operations (`DELETE`) are **soft deletes** (`IsActive = false`, `UpdatedAtUtc = UtcNow`). Records are never physically deleted from the database in order to preserve audit history and relational integrity.

---

## POST /api/catalog/categories

Creates a new category.

**Authentication**: Required (`Admin` role)

### Request Body
```json
{
  "name": "Gaming Laptops",
  "slug": "gaming-laptops"
}
```

- `name`: string, required, max length 120.
- `slug`: string, required, max length 140, format: URL-friendly (`^[a-z0-9]+(?:-[a-z0-9]+)*$`).

### Responses

- `201 Created`
  - Headers: `Location: /api/catalog/categories/{id}`
  - Body: empty or created category metadata.
- `400 Bad Request`: Validation failure (empty name, invalid slug format).
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `409 Conflict`: Slug already in use by another category.

---

## PUT /api/catalog/categories/{id}

Updates an existing category's name, slug, and active status.

**Authentication**: Required (`Admin` role)

### Request Body
```json
{
  "name": "High-End Gaming Laptops",
  "slug": "gaming-laptops",
  "isActive": true
}
```

### Responses

- `204 No Content`: Category successfully updated.
- `400 Bad Request`: Validation error in payload.
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Category with specified ID does not exist.
- `409 Conflict`: Slug is already taken by another category.

---

## DELETE /api/catalog/categories/{id}

Soft-deletes a category by setting `IsActive = false` and updating `UpdatedAtUtc`.
This operation is idempotent: if the category is already inactive, it returns `204 No Content`.
Products belonging to this category are not deleted, but they are excluded from public catalog queries.

**Authentication**: Required (`Admin` role)

### Responses

- `204 No Content`: Category soft-deleted (or already inactive).
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Category does not exist.

---

## POST /api/catalog/products

Creates a new product. Newly created products are automatically active (`IsActive = true`). Image upload is handled separately.

**Authentication**: Required (`Admin` role)

### Request Body
```json
{
  "categoryId": "9d16d27a-90bb-4bd3-ae8f-c846c9136d0a",
  "name": "Asus ROG Zephyrus G14",
  "description": "Ultraportable gaming laptop with AMD Ryzen and RTX graphics.",
  "price": 1899.99,
  "stockQuantity": 15
}
```

- `categoryId`: UUID, required (must reference an active category).
- `name`: string, required, max length 200.
- `description`: string, optional, max length 4000.
- `price`: decimal, required, `>= 0`.
- `stockQuantity`: integer, required, `>= 0`.

### Responses

- `201 Created`
  - Headers: `Location: /api/catalog/products/{id}`
  - Body: empty or created product metadata.
- `400 Bad Request`: Validation error or referenced category does not exist / is inactive.
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.

---

## PUT /api/catalog/products/{id}

Updates an existing product's metadata (`Name`, `Description`, `Price`, `CategoryId`, `IsActive`).
`StockQuantity` and `ImagePath` cannot be updated through this endpoint.

**Authentication**: Required (`Admin` role)

### Request Body
```json
{
  "categoryId": "9d16d27a-90bb-4bd3-ae8f-c846c9136d0a",
  "name": "Asus ROG Zephyrus G14 (2026 Edition)",
  "description": "Updated ultraportable gaming laptop.",
  "price": 1999.99,
  "isActive": true
}
```

### Responses

- `204 No Content`: Product updated successfully.
- `400 Bad Request`: Validation error or referenced category does not exist / is inactive.
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Product with specified ID does not exist.

---

## DELETE /api/catalog/products/{id}

Soft-deletes a product by setting `IsActive = false` and updating `UpdatedAtUtc`.
This operation is idempotent: if the product is already inactive, it returns `204 No Content`.
The product is immediately hidden from public read queries.

**Authentication**: Required (`Admin` role)

### Responses

- `204 No Content`: Product soft-deleted (or already inactive).
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Product does not exist.

---

## PATCH /api/catalog/products/{id}/stock

Sets the absolute stock quantity of a product.
Stock quantity can be updated for both active and inactive products (e.g. while preparing inventory).

**Authentication**: Required (`Admin` role)

### Request Body
```json
{
  "quantity": 30
}
```

- `quantity`: integer, required, `>= 0`.

### Responses

- `200 OK`
```json
{
  "productId": "11111111-1111-1111-1111-111111111111",
  "stockQuantity": 30
}
```
- `400 Bad Request`: `quantity < 0`.
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Product does not exist.

---

## POST /api/catalog/products/{id}/image

Uploads or replaces an image for the specified product. Can be performed on both active and inactive products.

If an image already exists for the product, it is replaced: the new image is saved, the database `ImagePath` is updated, and the previous physical file is removed from storage. If database persistence fails, the newly saved file is automatically cleaned up (failure compensation).

**Authentication**: Required (`Admin` role)

**Content-Type**: `multipart/form-data`

### Form Data
- `file`: binary file (required).
  - Supported formats: JPEG (`.jpg`, `.jpeg`), PNG (`.png`), WEBP (`.webp`).
  - Supported Content-Types: `image/jpeg`, `image/png`, `image/webp`.
  - Maximum size: 5 MiB (5,242,880 bytes).
  - Validation: file extension, Content-Type, and binary file signatures (magic bytes) must all match. Arbitrary files, SVG, executable files, or fake extensions are rejected.
  - Storage: saved under a cryptographically random server-generated GUID filename (client filename is never preserved on disk).

### Responses

- `200 OK`
```json
{
  "productId": "11111111-1111-1111-1111-111111111111",
  "imagePath": "/product-images/68a9dd8eb32540ff9e953de9983533d5.png"
}
```
- `400 Bad Request`: Empty file, unsupported file extension, mismatched Content-Type, invalid magic bytes signature, or file exceeding 5 MiB.
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Product does not exist.

---

## DELETE /api/catalog/products/{id}/image

Deletes the image associated with the product and sets `ImagePath = null`.

This operation is idempotent: if the product has no image (`ImagePath == null`), it returns `204 No Content`. If the physical file is already missing from disk, the database is still cleared and `204 No Content` is returned.

**Authentication**: Required (`Admin` role)

### Responses

- `204 No Content`: Image successfully removed (or already absent).
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Token lacks `Admin` role.
- `404 Not Found`: Product does not exist.

---

## GET /product-images/{filename}

Public static endpoint for serving product images directly from local storage.

**Authentication**: None (Public)

### Responses

- `200 OK`: Binary image stream with matching `Content-Type` header (`image/png`, `image/jpeg`, or `image/webp`).
- `404 Not Found`: File does not exist on disk or has been deleted.

