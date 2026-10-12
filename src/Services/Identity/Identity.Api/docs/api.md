# Identity API

Base URL for local development: `http://localhost:5001`. The API is also routed through the gateway at `http://localhost:5000` and is exposed under the same path prefixes.

## Authentication

This service issues JWT access tokens for the `Customer` and `Admin` roles. Requesting the profile requires a bearer token and the token must include the `sub`, `email`, `name`, and `role` claims.

```http
Authorization: Bearer <jwt>
```

## Endpoints

### Register a customer

```http
POST /api/auth/register
Content-Type: application/json
```

```json
{
  "email": "jane@example.com",
  "password": "Password123!",
  "fullName": "Jane Doe",
  "phone": "+1 555 123 4567"
}
```

Behavior:
- Email is normalized to lowercase.
- Passwords are hashed with BCrypt before storage.
- The role is always set to `Customer` via public registration.
- Duplicate emails return `409 Conflict`.

**200 OK** response:

```json
{
  "accessToken": "<jwt>",
  "expiresAtUtc": "2026-10-10T12:10:00Z",
  "user": {
    "id": "00000000-0000-0000-0000-000000000001",
    "email": "jane@example.com",
    "fullName": "Jane Doe",
    "phone": "+1 555 123 4567",
    "role": "Customer"
  }
}
```

### Login

```http
POST /api/auth/login
Content-Type: application/json
```

```json
{
  "email": "jane@example.com",
  "password": "Password123!"
}
```

- The response is the same as registration: a JWT plus user profile.
- Invalid credentials and unknown emails intentionally return the same `401 Unauthorized` message to avoid user enumeration.
- Disabled accounts return `403 Forbidden`.

### Current profile

```http
GET /api/auth/me
Authorization: Bearer <jwt>
```

Returns the authenticated user profile, including role and contact details.

**200 OK**

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "email": "jane@example.com",
  "fullName": "Jane Doe",
  "phone": "+1 555 123 4567",
  "role": "Customer"
}
```

## Notes

- `Jwt:Key`, `Jwt:Issuer`, and `Jwt:Audience` are configured via appsettings and validated on every call.
- The service uses EF Core and SQLite/SQL Server-compatible models; only the user table is persisted here.
- Password storage is intentionally one-way: the application never stores plaintext passwords.
