USE SneakerShop_Identity;
GO

CREATE TABLE dbo.Users (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email        NVARCHAR(256)    NOT NULL,   -- stored lower-case
    PasswordHash NVARCHAR(200)    NOT NULL,   -- BCrypt hash
    FullName     NVARCHAR(150)    NOT NULL,
    Phone        NVARCHAR(30)     NULL,
    Role         NVARCHAR(20)     NOT NULL,   -- Admin | Customer
    IsActive     BIT              NOT NULL,
    CreatedAt    DATETIME2        NOT NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO
-- The two demo accounts (admin and customer) are created by the Identity service on first start,
-- because their passwords must be hashed with BCrypt.
