# Catalog API

Base URL for local development: `http://localhost:5002`. The API is also routed through the gateway at `http://localhost:5000`.

Interactive Swagger UI is available at `http://localhost:5002/swagger` in Development. Unless stated otherwise, request and response bodies use JSON. JSON enum values are strings (`"Men"`, `"Women"`, `"Unisex"`, or `"Kids"`).

## Authentication

Brand and sneaker reads are public. Catalog mutations require a JWT with the `Admin` role:

```http
Authorization: Bearer <access-token>
```

The internal variant lookup is service-to-service only and requires the configured shared key:

```http
X-Internal-Key: <internal-api-key>
```

Do not expose the internal lookup through the public gateway.

## Endpoints

### List brands

```http
GET /api/brands
```

Returns brands sorted by name.

**200 OK**

```json
[
  { "id": 1, "name": "Nike", "slug": "nike" }
]
```

### Create a brand

```http
POST /api/brands
Authorization: Bearer <admin-token>
Content-Type: application/json
```

```json
{ "name": "New Balance" }
```

Names are trimmed; the slug is generated from the name.

**201 Created** returns a `BrandDto` with `id`, `name`, and `slug`.

| Status | Meaning |
| --- | --- |
| 400 | Invalid request body or name contains no letters or digits |
| 401 / 403 | Missing admin token or caller is not an admin |
| 409 | A brand with that name already exists |

### Search and list sneakers

```http
GET /api/sneakers
```

Optional query parameters:

| Parameter | Type | Behavior |
| --- | --- | --- |
| `search` | string | Case-insensitive/database-defined name match |
| `brandId` | integer | Filter by brand |
| `gender` | enum | `Men`, `Women`, `Unisex`, or `Kids` |
| `minPrice` | decimal | Inclusive minimum price |
| `maxPrice` | decimal | Inclusive maximum price |
| `sort` | string | `name` (default), `price_asc`, `price_desc`, or `newest` |
| `page` | integer | 1-based page; values below 1 are treated as 1 |
| `pageSize` | integer | Items per page; clamped to 1–100 (default 12) |

Only active sneakers are listed.

Example:

```http
GET /api/sneakers?brandId=1&gender=Men&minPrice=50&maxPrice=200&sort=price_asc&page=1&pageSize=12
```

**200 OK**

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "name": "Air Zoom",
      "slug": "air-zoom",
      "brand": "Nike",
      "gender": "Men",
      "price": 120.00,
      "imageUrl": null
    }
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 1,
  "totalPages": 1
}
```

### Get a sneaker

```http
GET /api/sneakers/{id}
```

Returns sneaker details, brand, and size/color variants. Inactive sneakers return `404 Not Found` to non-admin callers; admins can still view them.

**200 OK** returns a `SneakerDetail` with `id`, `name`, `slug`, `description`, `brand`, `gender`, `price`, `imageUrl`, `isActive`, `createdAt`, and `variants`.

**404 Not Found** is returned when the sneaker does not exist or is inactive and the caller is not an admin.

### Best sellers

```http
GET /api/sneakers/best-sellers?days=7&take=10
```

Active sneakers with the most pairs shipped over the last `days` days (1-30, default 7), best first, as `SneakerListItem`s. Counted from `OrderShipped` events into one Redis sorted set per day. Cached for 1 minute.

### Caching

Brands (30 minutes), sneaker lists (5 minutes) and sneaker details (10 minutes) are cached in memory and Redis (FusionCache). Admin writes remove the affected entries on every Catalog instance. The internal variant lookup used for order prices is never cached.

### Create a sneaker

```http
POST /api/sneakers
Authorization: Bearer <admin-token>
Content-Type: application/json
```

```json
{
  "name": "Air Zoom",
  "description": "Road running shoe",
  "brandId": 1,
  "gender": "Men",
  "price": 120.00,
  "imageUrl": "https://example.test/air-zoom.jpg"
}
```

`name` and `brandId` are required. `name` is limited to 200 characters, `description` to 2000, `imageUrl` to 500, and `price` must be from 0.01 through 100000. Slugs are generated from the name and must be unique.

**201 Created** returns the new sneaker detail.

| Status | Meaning |
| --- | --- |
| 400 | Invalid request, name contains no letters or digits, or brand does not exist |
| 401 / 403 | Missing admin token or caller is not an admin |
| 409 | A sneaker with the generated slug already exists |

### Update a sneaker

```http
PUT /api/sneakers/{id}
Authorization: Bearer <admin-token>
Content-Type: application/json
```

Uses the same complete request body and validation rules as create. The slug is regenerated from the new name.

**200 OK** returns the updated sneaker detail.

| Status | Meaning |
| --- | --- |
| 400 | Invalid request, name contains no letters or digits, or brand does not exist |
| 401 / 403 | Missing admin token or caller is not an admin |
| 404 | Sneaker does not exist |
| 409 | Another sneaker already uses the generated slug |

### Delete a sneaker

```http
DELETE /api/sneakers/{id}
Authorization: Bearer <admin-token>
```

Performs a soft delete: the sneaker becomes inactive and is removed from public listing, while existing order snapshots remain unchanged.

**204 No Content** on success; **404 Not Found** if the sneaker does not exist. Requires the admin role.

### Add one color to every active sneaker

```http
POST /api/sneakers/variants
Authorization: Bearer <admin-token>
Content-Type: application/json
```

```json
{ "color": "Black" }
```

`color` is required and trimmed (maximum length 50). For each active sneaker the API creates that color in every size the sneaker already sells. A sneaker with no variants gets sizes 8, 9, 10, and 11. A size/color pair that already exists is reused, and its id is returned without publishing another event. SKUs are generated from the sneaker slug, color, and size.

Each new variant publishes `SneakerVariantCreated`. Inventory creates an empty stock record (`quantity` 0) for that `variantId`.

**201 Created** when at least one variant is new. **200 OK** when every size/color pair already existed. Both return one row per active sneaker:

```json
[
  {
    "sneakerId": "00000000-0000-0000-0000-000000000002",
    "variantIds": [
      "00000000-0000-0000-0000-000000000011",
      "00000000-0000-0000-0000-000000000012"
    ]
  }
]
```

| Status | Meaning |
| --- | --- |
| 400 | Color is missing or blank |
| 401 / 403 | Missing admin token or caller is not an admin |

### Add a variant

```http
POST /api/sneakers/{id}/variants
Authorization: Bearer <admin-token>
Content-Type: application/json
```

```json
{ "sku": "AZ-900", "size": 9.5, "color": "Blue" }
```

`sku` and `color` are required (maximum lengths 64 and 50); `size` must be from 1 through 25. SKU is trimmed and stored uppercase; color is trimmed. A successful request publishes `SneakerVariantCreated` so Inventory can create an empty stock record.

**201 Created** returns a `VariantDto` containing `id`, `sku`, `size`, and `color`.

| Status | Meaning |
| --- | --- |
| 400 | Invalid request body |
| 401 / 403 | Missing admin token or caller is not an admin |
| 404 | Parent sneaker does not exist |
| 409 | SKU or the sneaker's size/color pair already exists |

### Internal variant lookup

```http
POST /internal/variants/lookup
X-Internal-Key: <internal-api-key>
Content-Type: application/json
```

Used by Ordering to retrieve authoritative names and prices. The endpoint is protected by the internal API key, not a user JWT.

```json
{
  "variantIds": [
    "00000000-0000-0000-0000-000000000001"
  ]
}
```

**200 OK** returns matching variants for active sneakers only. Duplicate IDs are ignored; unknown IDs and variants belonging to inactive sneakers are omitted.

```json
[
  {
    "variantId": "00000000-0000-0000-0000-000000000001",
    "sneakerId": "00000000-0000-0000-0000-000000000002",
    "sneakerName": "Air Zoom",
    "sku": "AZ-900",
    "size": 9.5,
    "color": "Blue",
    "unitPrice": 120.00
  }
]
```

**401 Unauthorized** is returned when the internal key is missing or invalid. This route is intended for internal service calls and is not exposed through the gateway.

### Health check

```http
GET /health
```

**200 OK** when the Catalog API health checks pass.

## Running tests

From the repository root:

```powershell
dotnet test src/Services/Catalog/Catalog.Api/test/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```
