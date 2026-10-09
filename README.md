# Sneaker Shop - .NET 8 Microservices Backend

An e-commerce backend for a sneaker seller, built with **.NET 8**, **ASP.NET Core Web API**, **EF Core + SQL Server**,
**RabbitMQ (MassTransit)**, **YARP** API gateway and **Docker Compose**.

## Architecture

```mermaid
flowchart LR
    Client[React / mobile client] --> GW[API Gateway - YARP :5000]
    GW --> ID[Identity API :5001]
    GW --> CAT[Catalog API :5002]
    GW --> INV[Inventory API :5003]
    GW --> ORD[Ordering API :5004]
    ID --- DB1[(SneakerShop_Identity)]
    CAT --- DB2[(SneakerShop_Catalog)]
    INV --- DB3[(SneakerShop_Inventory)]
    ORD --- DB4[(SneakerShop_Ordering)]
    ORD -- "HTTP: prices (internal key)" --> CAT
    CAT -. SneakerVariantCreated .-> MQ{{RabbitMQ}}
    ORD -. OrderCreated / OrderCancelled / OrderShipped .-> MQ
    MQ -. same events .-> INV
    INV -. StockReserved / StockReservationFailed .-> MQ
    MQ -. same events .-> ORD
```

| Service | Responsibility | Database |
|---|---|---|
| **ApiGateway** | Single public entry point, routing, per-IP rate limit, CORS | - |
| **Identity.Api** | Register/login, BCrypt password hashing, issues JWT (roles: Admin, Customer) | `SneakerShop_Identity` |
| **Catalog.Api** | Brands, sneakers, size/color variants, search and filters | `SneakerShop_Catalog` |
| **Inventory.Api** | Stock per variant, reservations, low-stock view | `SneakerShop_Inventory` |
| **Ordering.Api** | Orders and their lifecycle; reads real prices from Catalog | `SneakerShop_Ordering` |
| **SneakerShop.BuildingBlocks** | Shared library: event contracts, JWT setup, error handling, seed data | - |

Each service is a small layered app: **Controllers -> Services -> DbContext/Domain**.

## Run it

Requirements: Docker Desktop. (For `dotnet run` you also need the .NET 8 SDK, SQL Server and RabbitMQ.)

```bash
docker compose up --build
```

- Gateway: <http://localhost:5000> (use this from your frontend)
- Per-service Swagger UI (development only): <http://localhost:5001/swagger> (Identity), `:5002` Catalog, `:5003` Inventory, `:5004` Ordering
- RabbitMQ management: <http://localhost:15672> (guest / guest)
- SQL Server: `localhost,1433`, user `sa`, password `Sneaker_Shop123` (override with `SA_PASSWORD`)

Demo accounts (created on first start; **change them outside local development**):

| Role | Email | Password |
|---|---|---|
| Admin | admin@sneakershop.local | Admin@12345 |
| Customer | customer@sneakershop.local | Customer@12345 |

Try the full flow with [`docs/requests.http`](docs/requests.http).

### Without Docker

Start SQL Server and RabbitMQ yourself, then in five terminals:

```bash
dotnet run --project src/Services/Identity/Identity.Api     # :5001
dotnet run --project src/Services/Catalog/Catalog.Api       # :5002
dotnet run --project src/Services/Inventory/Inventory.Api   # :5003
dotnet run --project src/Services/Ordering/Ordering.Api     # :5004
dotnet run --project src/Gateway/ApiGateway                 # :5000
```

## Public API (through the gateway)

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | public | Create a customer account |
| POST | `/api/auth/login` | public | Get a JWT |
| GET | `/api/auth/me` | user | Current profile |
| GET | `/api/brands` | public | List brands |
| POST | `/api/brands` | admin | Create brand |
| GET | `/api/sneakers` | public | Search/filter/sort/page (`search, brandId, gender, minPrice, maxPrice, sort, page, pageSize`) |
| GET | `/api/sneakers/{id}` | public | Detail with variants |
| POST / PUT / DELETE | `/api/sneakers[/{id}]` | admin | Create / update / soft-delete |
| POST | `/api/sneakers/{id}/variants` | admin | Add size/color (creates stock record via event) |
| GET | `/api/inventory/{variantId}/availability` | public | Pairs available |
| POST | `/api/inventory/availability` | public | Availability for many variants |
| GET | `/api/inventory` | admin | Stock list, `maxAvailable` for low stock |
| PUT | `/api/inventory/{variantId}` | admin | Set quantity on hand |
| POST | `/api/orders` | user | Place an order |
| GET | `/api/orders/my` | user | My orders |
| GET | `/api/orders/{id}` | owner/admin | One order |
| POST | `/api/orders/{id}/cancel` | owner/admin | Cancel (Pending or Confirmed) |
| GET | `/api/orders` | admin | All orders, filter by `status` |
| PUT | `/api/orders/{id}/status` | admin | `Shipped` or `Delivered` |

## How an order flows (choreographed saga)

1. `POST /api/orders` - Ordering asks Catalog for the real name/price of each variant (never trusts client prices), stores the order as **Pending**, publishes `OrderCreated`.
2. Inventory reserves stock for all lines (all-or-nothing) and publishes `StockReserved` or `StockReservationFailed`.
3. Ordering moves the order to **Confirmed** or **Rejected** (with a reason).
4. Cancel -> `OrderCancelled` -> Inventory releases the reservation. Ship -> `OrderShipped` -> Inventory deducts the stock permanently.

Order state machine: `Pending -> Confirmed -> Shipped -> Delivered`, with `Pending -> Rejected` and `Pending/Confirmed -> Cancelled`.

## Design decisions

- **Database per service** - no cross-database foreign keys; services share only IDs and events.
- **Prices are snapshotted** into `OrderItems`, so catalog changes never alter past orders.
- **Optimistic concurrency** (`RowVersion`) on stock and orders; MassTransit retries with exponential backoff.
- **Idempotent consumers** - a redelivered event does not reserve or release stock twice.
- **Internal endpoint** `/internal/variants/lookup` is protected by a shared key and is not routed by the gateway.
- **JWT** is validated by every service with the same key; roles are checked with `[Authorize(Roles = ...)]`.
- Errors are returned as RFC 7807 `ProblemDetails`.

## Before going to production

- Replace `EnsureCreated` with **EF Core migrations** (`dotnet ef migrations add ...` per service).
- Move secrets (`Jwt:Key`, `Internal:ApiKey`, SQL password, demo accounts) to a secret store; use HTTPS.
- Add the **transactional outbox** (MassTransit EF outbox) so an order and its event are saved atomically.
- Add a Payment service, email notifications, structured logging/tracing (OpenTelemetry) and automated tests.
- Only expose the gateway; do not publish the per-service ports.
