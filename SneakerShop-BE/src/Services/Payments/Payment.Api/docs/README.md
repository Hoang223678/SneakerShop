# Payment API

Payment API runs at `http://localhost:5005` locally and is routed through the gateway at `http://localhost:5000`.
Except for the provider webhook and `/health`, payment endpoints require a SneakerShop JWT.
Amounts and expiry always come from Ordering; callers cannot set either value.

## `POST /api/payments`

Create a payment for a confirmed order or resume the current pending attempt.

**Access:** authenticated order owner  
**Success:** `201 Created`; `Location` points to `GET /api/payments/{id}`.

```http
POST /api/payments
Authorization: Bearer <customer-token>
Content-Type: application/json

{
  "orderId": "8d95770d-2997-440c-a356-6a1ab7d1e3ac",
  "method": "vnpay"
}
```

Use `vnpay` (or `VnPay`) to receive a signed VNPay sandbox checkout URL. Use `Fake` only in Development. An order must be `Confirmed`, owned by the caller and still within its payment window. Repeating the request for a pending attempt returns the existing attempt instead of creating another. After VNPay reports success, Ordering marks the order paid and moves it to `Shipped`.

The response contains `id`, `orderId`, authoritative `amount`, `currency`, `status`, `expiresAt`, `attemptId`, and `paymentUrl`.

**Errors:** `400` unsupported method or Fake outside Development; `404` missing/non-owned order; `409` order not confirmed, expired or already paid; `502` provider could not create a payment URL.

## `GET /api/payments/{id}`

Read a payment and its newest attempt.

**Access:** payment owner or Admin  
**Success:** `200 OK` with a `PaymentResponse`.

```http
GET /api/payments/38ad8fc9-4025-42c6-a5ca-5a845e32de7b
Authorization: Bearer <customer-token>
```

**Errors:** `404 Not Found` when the payment does not exist or the caller is not its owner.

## `POST /api/payments/{id}/refund`

Request a full or partial refund. The provider refund runs asynchronously from the `RefundRequested` event.

**Access:** Admin  
**Success:** `202 Accepted`; `Location` points to the payment status resource.

```http
POST /api/payments/38ad8fc9-4025-42c6-a5ca-5a845e32de7b/refund
Authorization: Bearer <admin-token>
Idempotency-Key: 6c5db79a-a045-43fa-af17-43f359468dec
Content-Type: application/json

{
  "amount": 25000
}
```

Send a unique GUID `Idempotency-Key` for each refund; reuse that key only to retry the same refund. Amount must be positive and no greater than the remaining refundable balance.

**Errors:** `400` invalid key/amount; `404` unknown payment; `409` payment is not successful, key reused with a different amount, or refund exceeds the remaining balance.

## `GET /api/payments/webhooks/vnpay`

VNPay server-to-server IPN callback. This endpoint is anonymous and limited to 60 requests/minute per source IP. Configure the merchant IPN URL to this path at the public gateway address.

The service validates the VNPay HMAC-SHA512 signature with a constant-time comparison, merchant code, currency, attempt reference, provider transaction ID and amount before applying a result. Repeated callbacks are idempotent.

## `GET /api/payments/vnpay/return`

Browser return from the VNPay sandbox. The signed query is verified the same way as IPN. If `Payments:VnPay:FrontendResultUrl` is set, the browser is redirected there with `status` and `attemptId`; otherwise the API returns a simple HTML result page. Sandbox return payloads often omit `vnp_CurrCode` — that field is only rejected when present and not `VND`. Configure `vnp_ReturnUrl` to this path (default `http://localhost:5000/api/payments/vnpay/return`).

**Success:** VNPay acknowledgement JSON:

```json
{ "RspCode": "00", "Message": "Confirm Success" }
```

**Errors:** `400` invalid signature/payload or mismatched amount; `404` unknown attempt; `409` provider transaction ID was already used.

## `POST /api/payments/dev/attempts/{attemptId}/succeed`

Development-only endpoint to simulate the Fake provider completing payment.

**Access:** authenticated owner of the Fake attempt  
**Success:** `204 No Content`. The service publishes `PaymentSucceeded`; the saga updates the order asynchronously.

```http
POST /api/payments/dev/attempts/38ad8fc9-4025-42c6-a5ca-5a845e32de7b/succeed
Authorization: Bearer <customer-token>
```

Returns `404` when the app is not in Development or the attempt is missing/not owned by the caller.

## `GET /health`

Health-check endpoint; no authentication required. Returns `200 OK` when the API process is responding.

## Payment status and events

Payment statuses are `Pending`, `Succeeded`, `Failed`, `Expired`, and `Refunded`. Provider IPN and reconciliation—not a browser redirect—are authoritative. Verified outcomes publish `PaymentSucceeded` or `PaymentFailed` through the EF outbox. Refund processing publishes `PaymentRefunded`; a full refund lets Ordering cancel the paid order and release reserved stock.

## Local setup

Local Development uses the public VNPay sandbox sample merchant (`2QXUI4J4`). Override `VNPAY_TMN_CODE` and `VNPAY_HASH_SECRET` with your own sandbox credentials from https://sandbox.vnpayment.vn/devreg/ when you have them. Never put production merchant secrets or card data in source control.
