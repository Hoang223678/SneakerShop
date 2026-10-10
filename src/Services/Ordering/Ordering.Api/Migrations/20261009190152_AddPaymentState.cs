using Microsoft.EntityFrameworkCore.Migrations;
using SneakerShop.BuildingBlocks.Data;

#nullable disable

namespace Ordering.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.Orders', N'IdempotencyKey') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [IdempotencyKey] nvarchar(128) NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [dbo].[Orders]
                SET [IdempotencyKey] = CONCAT(N'legacy-', REPLACE(CONVERT(nvarchar(36), [Id]), N'-', N''))
                WHERE [IdempotencyKey] IS NULL;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[Orders]')
                      AND [name] = N'IdempotencyKey'
                      AND [is_nullable] = 1)
                    ALTER TABLE [dbo].[Orders] ALTER COLUMN [IdempotencyKey] nvarchar(128) NOT NULL;

                IF COL_LENGTH(N'dbo.Orders', N'IsPaid') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [IsPaid] bit NOT NULL
                        CONSTRAINT [DF_Orders_IsPaid] DEFAULT (0);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Orders_UserId' AND [object_id] = OBJECT_ID(N'[dbo].[Orders]'))
                    CREATE INDEX [IX_Orders_UserId] ON [dbo].[Orders] ([UserId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Orders_Status' AND [object_id] = OBJECT_ID(N'[dbo].[Orders]'))
                    CREATE INDEX [IX_Orders_Status] ON [dbo].[Orders] ([Status]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UQ_Orders_UserId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[Orders]'))
                    CREATE UNIQUE INDEX [UQ_Orders_UserId_IdempotencyKey]
                        ON [dbo].[Orders] ([UserId], [IdempotencyKey]);
                """);
            migrationBuilder.Sql(MassTransitOutboxSchema.CreateMissingTables);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @defaultConstraint sysname;
                SELECT @defaultConstraint = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.[default_object_id] = dc.[object_id]
                WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[Orders]')
                  AND c.[name] = N'IsPaid';
                IF @defaultConstraint IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [' + @defaultConstraint + N']');
                IF COL_LENGTH(N'dbo.Orders', N'IsPaid') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP COLUMN [IsPaid];
                """);
        }
    }
}
