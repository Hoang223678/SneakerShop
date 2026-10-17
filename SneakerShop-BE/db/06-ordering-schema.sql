USE SneakerShop_Ordering;
GO

CREATE TABLE dbo.Orders (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    UserId          UNIQUEIDENTIFIER NOT NULL,   -- Identity's User.Id (no cross-database FK)
    Status          NVARCHAR(20)     NOT NULL,   -- Pending | Confirmed | Rejected | Cancelled | Shipped | Delivered
    TotalAmount     DECIMAL(18,2)    NOT NULL,
    PaymentId       UNIQUEIDENTIFIER NULL,
    PaymentExpiresAt DATETIME2       NOT NULL,
    ShippingName    NVARCHAR(150)    NOT NULL,
    ShippingPhone   NVARCHAR(30)     NOT NULL,
    ShippingAddress NVARCHAR(500)    NOT NULL,
    FailureReason   NVARCHAR(500)    NULL,
    CreatedAt       DATETIME2        NOT NULL,
    UpdatedAt       DATETIME2        NOT NULL,
    RowVersion      ROWVERSION       NOT NULL
);
CREATE INDEX IX_Orders_UserId ON dbo.Orders (UserId);
CREATE INDEX IX_Orders_Status ON dbo.Orders (Status);

-- Product data is copied (snapshot) so later catalog changes never alter historical orders.
CREATE TABLE dbo.OrderItems (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId     UNIQUEIDENTIFIER NOT NULL,
    VariantId   UNIQUEIDENTIFIER NOT NULL,
    SneakerId   UNIQUEIDENTIFIER NOT NULL,
    SneakerName NVARCHAR(200)    NOT NULL,
    Sku         NVARCHAR(64)     NOT NULL,
    Size        DECIMAL(4,1)     NOT NULL,
    Color       NVARCHAR(50)     NOT NULL,
    UnitPrice   DECIMAL(18,2)    NOT NULL,
    Quantity    INT              NOT NULL,
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id) ON DELETE CASCADE
);
CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems (OrderId);
GO
