USE SneakerShop_Payment;
GO

CREATE TABLE dbo.Payments (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    UserId          UNIQUEIDENTIFIER NOT NULL,
    Amount          DECIMAL(18,2)    NOT NULL,
    Currency        NVARCHAR(3)      NOT NULL,
    Status          NVARCHAR(20)     NOT NULL,
    AmountRefunded  DECIMAL(18,2)    NOT NULL,
    ExpiresAt       DATETIME2        NOT NULL,
    CreatedAt       DATETIME2        NOT NULL
);
CREATE UNIQUE INDEX IX_Payments_OrderId ON dbo.Payments (OrderId);
CREATE INDEX IX_Payments_Status_ExpiresAt ON dbo.Payments (Status, ExpiresAt);

CREATE TABLE dbo.PaymentAttempts (
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentAttempts PRIMARY KEY,
    PaymentId             UNIQUEIDENTIFIER NOT NULL,
    Provider              NVARCHAR(30)     NOT NULL,
    ProviderTransactionId NVARCHAR(100)    NULL,
    PaymentUrl            NVARCHAR(2048)   NULL,
    Status                NVARCHAR(20)     NOT NULL,
    ExpiresAt             DATETIME2        NOT NULL,
    RawPayload            NVARCHAR(MAX)    NULL,
    CreatedAt             DATETIME2        NOT NULL,
    CONSTRAINT FK_PaymentAttempts_Payments FOREIGN KEY (PaymentId)
        REFERENCES dbo.Payments (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_PaymentAttempts_Provider_ProviderTransactionId
    ON dbo.PaymentAttempts (Provider, ProviderTransactionId)
    WHERE ProviderTransactionId IS NOT NULL;
CREATE INDEX IX_PaymentAttempts_PaymentId_CreatedAt ON dbo.PaymentAttempts (PaymentId, CreatedAt);

CREATE TABLE dbo.PaymentRefunds (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentRefunds PRIMARY KEY,
    PaymentId   UNIQUEIDENTIFIER NOT NULL,
    Amount      DECIMAL(18,2)    NOT NULL,
    Completed   BIT              NOT NULL,
    CreatedAt   DATETIME2        NOT NULL,
    CONSTRAINT FK_PaymentRefunds_Payments FOREIGN KEY (PaymentId)
        REFERENCES dbo.Payments (Id) ON DELETE CASCADE
);
CREATE INDEX IX_PaymentRefunds_PaymentId ON dbo.PaymentRefunds (PaymentId);
GO
