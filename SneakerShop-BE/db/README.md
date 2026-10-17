# Database scripts

Each microservice owns its own database (**database-per-service**). No SQL foreign keys cross databases:
services only share IDs (e.g. `VariantId`) and talk through HTTP and events.

You do **not** need to run these scripts manually with Docker Compose: the `database-init` service creates
the service databases, and the APIs apply their EF Core migrations and seed data on first start.
Use the scripts if you want to create the databases yourself or inspect the schema.

## Run order

| # | Script | Purpose |
|---|--------|---------|
| 0 | `00-create-databases.sql` | Creates the 5 databases |
| 1 | `01-identity-schema.sql` | Users |
| 2 | `02-catalog-schema.sql` | Brands, Sneakers, SneakerVariants |
| 3 | `03-catalog-seed.sql` | 4 brands, 6 sneakers, 24 variants |
| 4 | `04-inventory-schema.sql` | StockItems, StockReservations |
| 5 | `05-inventory-seed.sql` | 20 pairs per seeded variant |
| 6 | `06-ordering-schema.sql` | Orders, OrderItems |

```bash
sqlcmd -C -S localhost,1433 -U sa -P "12345" -i 00-create-databases.sql
# ...then the remaining files in order
```

The demo users are created by the Identity service on first start because their passwords must be hashed with BCrypt.
If you create the schema with these scripts, the services detect existing tables and only add seed data when a table is empty.

## Entity relationships

```mermaid
erDiagram
    BRANDS ||--o{ SNEAKERS : has
    SNEAKERS ||--o{ SNEAKER_VARIANTS : "has sizes/colors"
    ORDERS ||--|{ ORDER_ITEMS : contains

    BRANDS { int Id PK
             string Name
             string Slug }
    SNEAKERS { guid Id PK
               string Name
               string Slug
               int BrandId FK
               string Gender
               decimal Price
               bool IsActive }
    SNEAKER_VARIANTS { guid Id PK
                       guid SneakerId FK
                       string Sku
                       decimal Size
                       string Color }
    STOCK_ITEMS { guid VariantId PK
                  int Quantity
                  int Reserved
                  rowversion RowVersion }
    STOCK_RESERVATIONS { guid Id PK
                         guid OrderId
                         guid VariantId
                         int Quantity
                         string Status }
    USERS { guid Id PK
            string Email
            string PasswordHash
            string Role }
    ORDERS { guid Id PK
             guid UserId
             string Status
             decimal TotalAmount }
    ORDER_ITEMS { guid Id PK
                  guid OrderId FK
                  guid VariantId
                  string SneakerName
                  decimal UnitPrice
                  int Quantity }
```

Logical (not enforced) links across databases: `STOCK_ITEMS.VariantId` = `SNEAKER_VARIANTS.Id`,
`ORDER_ITEMS.VariantId` = `SNEAKER_VARIANTS.Id`, `ORDERS.UserId` = `USERS.Id`.
