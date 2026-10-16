# Ordering API

Ordering API runs at `http://localhost:5004` locally and through the gateway at `http://localhost:5000`.
Public order routes require a SneakerShop JWT. Customers may access only their own orders; Admin users can manage and inspect all orders.

## Order lifecycle

Orders start as `Pending` while Inventory reserves stock. Inventory confirmation moves an order to `Confirmed`; reservation failure makes it `Rejected`. Confirmed orders can be paid through Payment API, which publishes verified payment events. A successful payment marks the order paid, moves it to `Shipped`, and publishes `OrderPaid` plus `OrderShipped` so Inventory commits reserved stock. Shipped orders can then be marked `Delivered`. Orders in `Pending` or `Confirmed` can be cancelled; cancelling a paid `Confirmed` order also requests a full refund.

`PaymentExpiresAt` is set when the order is created and shared with Inventory and Payment. Prices always come from Catalog, never from client-submitted prices. Creating an order requires an idempotency key.

## `POST /api/orders`

Place an order. Stock reservation happens asynchronously.

**Access:** authenticated customer  
**Required header:** `Idempotency-Key`, non-empty and at most 128 characters. Reuse it to retry the same create request; the existing order will be returned.  
**Success:** `201 Created` with a `Location` header for `GET /api/orders/{id}`.

```http
POST /api/orders
Authorization: Bearer <customer-token>
Idempotency-Key: 4e24cb68-1950-4338-baf0-f58382a18a1c
Content-Type: application/json

{
  "shippingName": "Demo Customer",
  "shippingPhone": "0900000000",
  "shippingAddress": "1 Example Street, Ho Chi Minh City",
  "items": [
    { "variantId": "<variant-id-from-catalog>", "quantity": 2 }
  ]
}
```

Replace `<variant-id-from-catalog>` with a real variant ID returned in a sneaker's `variants` array by `GET /api/sneakers/{sneakerId}` (or find a sneaker first with `GET /api/sneakers`). The placeholder above is not a literal UUID. Ordering returns `400` if a variant does not exist or belongs to an inactive sneaker. Each request supports 1-20 item lines; quantity per line must be 1-1000000. Duplicate lines for a variant are combined, with a maximum of 1000000 pairs per variant after combining. The API fetches authoritative product data and prices from Catalog.

**Errors:** `400` invalid/missing idempotency key, invalid quantities, too many merged pairs or unavailable variants; `503` Catalog unavailable; `502` Catalog returned an unexpected error.

## `GET /api/orders/my`

List the authenticated customer's orders, newest first.

**Access:** authenticated customer  
**Success:** `200 OK` with an array of `OrderDto`.

```http
GET /api/orders/my
Authorization: Bearer <customer-token>
```

## `GET /api/orders/{id}`

Get order details and item snapshots.

**Access:** order owner or Admin  
**Success:** `200 OK` with an `OrderDto`.

```http
GET /api/orders/8d95770d-2997-440c-a356-6a1ab7d1e3ac
Authorization: Bearer <customer-token>
```

**Errors:** `404 Not Found` when the order does not exist or belongs to another customer.

## `POST /api/orders/{id}/cancel`

Cancel an order if it is `Pending` or `Confirmed`. Inventory receives `OrderCancelled` and releases any reservation. If the order was paid, the API also requests a full refund through the payment workflow.

**Access:** order owner or Admin  
**Success:** `200 OK` with the updated `OrderDto`.

```http
POST /api/orders/8d95770d-2997-440c-a356-6a1ab7d1e3ac/cancel
Authorization: Bearer <customer-token>
```

**Errors:** `404 Not Found` for a missing/non-owned order; `409 Conflict` when the current order status does not allow cancellation.

## `GET /api/orders`

List orders for operations, with optional status filter and pagination.

**Access:** Admin  
**Success:** `200 OK` with `PagedResult<OrderDto>`: `items`, `page`, `pageSize`, `totalCount`, and computed `totalPages`.

```http
GET /api/orders?status=Confirmed&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

`status` accepts `Pending`, `Confirmed`, `Rejected`, `Cancelled`, `Shipped`, or `Delivered`. Page is clamped to at least 1 and page size to 1-100 (default 20).

## `PUT /api/orders/{id}/status`

Admin-only manual status action. Set `Shipped` for a paid `Confirmed` order or `Delivered` for a `Shipped` order. Other statuses are driven by the order workflow and cannot be set manually. Shipping publishes `OrderShipped` so Inventory commits reserved stock.

**Access:** Admin  
**Success:** `200 OK` with the updated `OrderDto`.

```http
PUT /api/orders/8d95770d-2997-440c-a356-6a1ab7d1e3ac/status
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "status": "Shipped"
}
```

**Errors:** `400` unsupported status; `404` unknown order; `409` unpaid shipment or invalid transition.

## Internal `GET /internal/orders/{id}/payment-details`

Service-to-service route used by Payment API to validate the order owner, status, authoritative amount, and payment expiry. It is not exposed by the API gateway and requires the `X-Internal-Key` header.

**Success:** `200 OK` with `orderId`, `userId`, `status`, `amount`, and `paymentExpiresAt`.  
**Errors:** `401 Unauthorized` for missing/invalid internal key; `404 Not Found` for an unknown order.

## `GET /health`

Health check; no authentication required. Returns `200 OK` when Ordering API is responding.

## Order response

`OrderDto` includes the order ID and owner, `status`, `isPaid`, optional `paymentId`, authoritative `totalAmount`, `paymentExpiresAt`, shipping details, optional failure reason, timestamps, and item snapshots. Each item includes variant/product IDs, name/SKU, size/color, unit price, quantity, and line total. Enums are serialized as strings.
