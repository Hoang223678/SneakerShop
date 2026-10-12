# Inventory API

Base URL for local development: `http://localhost:5003`. The API is also routed through the gateway at `http://localhost:5000`.

## Authentication

Availability reads are public. Admin-only endpoints require a JWT with the `Admin` role.

```http
Authorization: Bearer <jwt>
```

## Endpoints

### Check availability for one variant

```http
GET /api/inventory/{variantId}/availability
```

Returns the current number of pairs available for a single variant.

**200 OK**

```json
{
  "variantId": "00000000-0000-0000-0000-000000000001",
  "available": 4,
  "inStock": true
}
```

### Check availability for many variants

```http
POST /api/inventory/availability
Content-Type: application/json
```

```json
{
  "variantIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

Returns a list of `StockAvailability` objects for every requested variant that exists in stock.

### List stock as an admin

```http
GET /api/inventory?maxAvailable=5&page=1&pageSize=20
Authorization: Bearer <jwt>
```

Optional query parameters:

| Parameter | Type | Behavior |
| --- | --- | --- |
| `maxAvailable` | int | Return only variants whose `quantity - reserved <= maxAvailable` |
| `page` | int | 1-based paging, minimum 1 |
| `pageSize` | int | 1..100, default 20 |

**200 OK** returns a paged list of stock items with the fields `variantId`, `sku`, `quantity`, `reserved`, `available`, and `updatedAt`.

### Set stock quantity

```http
PUT /api/inventory/{variantId}
Authorization: Bearer <jwt>
Content-Type: application/json
```

```json
{
  "quantity": 25
}
```

- `quantity` must be at least the current reserved pairs count.
- A conflict is raised when another request changes the stock while this update is in progress.

**200 OK** returns the updated stock item data.

## Reservation lifecycle

This service also processes expiring reservations in the background. Every 30 seconds, the `ReservationExpiryService` looks for `Reserved` records whose `ExpiresAt` is in the past, decrements the relevant `StockItem.Reserved` count, changes the reservation to `Expired`, and publishes `OrderReservationExpired`.

This protects the system from unpaid orders that never complete payment, and it keeps available quantities consistent with the current reservation timeline.

## Notes

- Availability is calculated with `Quantity - Reserved`.
- `StockItem.RowVersion` and `StockReservation.RowVersion` use optimistic concurrency to prevent lost updates.
- Reservation and cancellation events are persisted with the MassTransit EF outbox so message delivery is recoverable if the broker is briefly unavailable.
