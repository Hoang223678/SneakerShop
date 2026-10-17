USE SneakerShop_Catalog;
GO

CREATE TABLE dbo.Brands (
    Id   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Brands PRIMARY KEY,
    Name NVARCHAR(100)     NOT NULL,
    Slug NVARCHAR(120)     NOT NULL,
    CONSTRAINT UQ_Brands_Name UNIQUE (Name)
);

CREATE TABLE dbo.Sneakers (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Sneakers PRIMARY KEY,
    Name        NVARCHAR(200)    NOT NULL,
    Slug        NVARCHAR(220)    NOT NULL,
    Description NVARCHAR(2000)   NULL,
    BrandId     INT              NOT NULL,
    Gender      NVARCHAR(20)     NOT NULL,   -- Men | Women | Unisex | Kids
    Price       DECIMAL(18,2)    NOT NULL,
    ImageUrl    NVARCHAR(500)    NULL,
    IsActive    BIT              NOT NULL,
    CreatedAt   DATETIME2        NOT NULL,
    UpdatedAt   DATETIME2        NULL,
    CONSTRAINT UQ_Sneakers_Slug UNIQUE (Slug),
    CONSTRAINT FK_Sneakers_Brands FOREIGN KEY (BrandId) REFERENCES dbo.Brands (Id)
);
CREATE INDEX IX_Sneakers_BrandId ON dbo.Sneakers (BrandId);

CREATE TABLE dbo.SneakerVariants (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SneakerVariants PRIMARY KEY,
    SneakerId UNIQUEIDENTIFIER NOT NULL,
    Sku       NVARCHAR(64)     NOT NULL,
    Size      DECIMAL(4,1)     NOT NULL,   -- US size
    Color     NVARCHAR(50)     NOT NULL,
    CONSTRAINT UQ_SneakerVariants_Sku UNIQUE (Sku),
    CONSTRAINT UQ_SneakerVariants_Sneaker_Size_Color UNIQUE (SneakerId, Size, Color),
    CONSTRAINT FK_SneakerVariants_Sneakers FOREIGN KEY (SneakerId) REFERENCES dbo.Sneakers (Id) ON DELETE CASCADE
);
GO
