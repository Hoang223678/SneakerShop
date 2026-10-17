USE SneakerShop_Inventory;
GO

-- VariantId is the Id of Catalog's SneakerVariant. There is intentionally no foreign key across databases.
CREATE TABLE dbo.StockItems (
    VariantId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockItems PRIMARY KEY,
    Sku        NVARCHAR(64)     NULL,
    Quantity   INT              NOT NULL,   -- pairs on hand
    Reserved   INT              NOT NULL,   -- pairs held for open orders; available = Quantity - Reserved
    UpdatedAt  DATETIME2        NOT NULL,
    RowVersion ROWVERSION       NOT NULL,   -- optimistic concurrency
    CONSTRAINT CK_StockItems_NonNegative CHECK (Quantity >= 0 AND Reserved >= 0)
);

CREATE TABLE dbo.StockReservations (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockReservations PRIMARY KEY,
    OrderId   UNIQUEIDENTIFIER NOT NULL,
    VariantId UNIQUEIDENTIFIER NOT NULL,
    Quantity  INT              NOT NULL,
    Status    NVARCHAR(20)     NOT NULL,   -- Reserved | Committed | Released
    CreatedAt DATETIME2        NOT NULL,
    UpdatedAt DATETIME2        NULL
);
CREATE INDEX IX_StockReservations_OrderId   ON dbo.StockReservations (OrderId);
CREATE INDEX IX_StockReservations_VariantId ON dbo.StockReservations (VariantId);
GO
