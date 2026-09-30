# Auth API

Base path: `/api/auth`

---

## POST /api/auth/register

Creates a new user account with the **Customer** role.

**Authentication**: None

**Request body**:
```json
{
  "email": "user@example.com",
  "password": "SecurePass1"
}
```

**Success**: `201 Created`
```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<opaque-token>",
  "accessTokenExpiresAtUtc": "2026-01-01T00:15:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "roles": ["Customer"]
  }
}
```

**Errors**:
| Status | Reason |
|---|---|
| 400 | Validation error or password policy violation |
| 409 | Email already registered |

---

## POST /api/auth/login

Authenticates an existing user.

**Authentication**: None

**Request body**:
```json
{
  "email": "user@example.com",
  "password": "SecurePass1"
}
```

**Success**: `200 OK`
```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<opaque-token>",
  "accessTokenExpiresAtUtc": "2026-01-01T00:15:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "roles": ["Customer"]
  }
}
```

**Errors**:
| Status | Reason |
|---|---|
| 401 | Invalid email or password |

---

## POST /api/auth/refresh

Exchanges a valid refresh token for a new access/refresh token pair (rotation).

**Authentication**: None

**Request body**:
```json
{
  "refreshToken": "<opaque-token>"
}
```

**Success**: `200 OK`
```json
{
  "accessToken": "<new-jwt>",
  "refreshToken": "<new-opaque-token>",
  "accessTokenExpiresAtUtc": "2026-01-01T00:15:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "roles": ["Customer"]
  }
}
```

The previous refresh token is revoked after successful rotation.

**Errors**:
| Status | Reason |
|---|---|
| 401 | Invalid, expired, or already-revoked refresh token |

---

## POST /api/auth/logout

Revokes the provided refresh token.

**Authentication**: None (token-based revocation)

**Request body**:
```json
{
  "refreshToken": "<opaque-token>"
}
```

**Success**: `204 No Content`

Idempotent — revoking an already-revoked token does not produce an error.

---

## GET /api/auth/me

Returns the currently authenticated user's profile.

**Authentication**: Bearer JWT required

**Request body**: None

**Success**: `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "roles": ["Customer"]
}
```

**Errors**:
| Status | Reason |
|---|---|
| 401 | Missing or invalid JWT |
