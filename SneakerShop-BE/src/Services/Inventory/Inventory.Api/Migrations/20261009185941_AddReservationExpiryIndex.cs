using Microsoft.EntityFrameworkCore.Migrations;
using SneakerShop.BuildingBlocks.Data;

#nullable disable

namespace Inventory.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationExpiryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.StockReservations', N'ExpiresAt') IS NULL
                    ALTER TABLE [dbo].[StockReservations] ADD [ExpiresAt] datetime2 NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [dbo].[StockReservations]
                SET [ExpiresAt] = DATEADD(minute, 15, [CreatedAt])
                WHERE [ExpiresAt] IS NULL;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[StockReservations]')
                      AND [name] = N'ExpiresAt'
                      AND [is_nullable] = 1)
                    ALTER TABLE [dbo].[StockReservations] ALTER COLUMN [ExpiresAt] datetime2 NOT NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.StockReservations', N'RowVersion') IS NULL
                    ALTER TABLE [dbo].[StockReservations] ADD [RowVersion] rowversion NOT NULL;

                IF OBJECT_ID(N'[dbo].[OrderCancellations]', N'U') IS NULL
                    CREATE TABLE [dbo].[OrderCancellations] (
                        [OrderId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_OrderCancellations] PRIMARY KEY ([OrderId])
                    );

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StockReservations_Status_ExpiresAt' AND [object_id] = OBJECT_ID(N'[dbo].[StockReservations]'))
                    DROP INDEX [IX_StockReservations_Status_ExpiresAt] ON [dbo].[StockReservations];
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StockReservations_Status_ExpiresAt' AND [object_id] = OBJECT_ID(N'[dbo].[StockReservations]'))
                    CREATE INDEX [IX_StockReservations_Status_ExpiresAt]
                        ON [dbo].[StockReservations] ([Status], [ExpiresAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StockReservations_OrderId' AND [object_id] = OBJECT_ID(N'[dbo].[StockReservations]'))
                    CREATE INDEX [IX_StockReservations_OrderId] ON [dbo].[StockReservations] ([OrderId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StockReservations_VariantId' AND [object_id] = OBJECT_ID(N'[dbo].[StockReservations]'))
                    CREATE INDEX [IX_StockReservations_VariantId] ON [dbo].[StockReservations] ([VariantId]);
                """);
            migrationBuilder.Sql(MassTransitOutboxSchema.CreateMissingTables);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StockReservations_Status_ExpiresAt' AND [object_id] = OBJECT_ID(N'[dbo].[StockReservations]'))
                BEGIN
                    DROP INDEX [IX_StockReservations_Status_ExpiresAt] ON [dbo].[StockReservations];
                    CREATE INDEX [IX_StockReservations_Status_ExpiresAt]
                        ON [dbo].[StockReservations] ([ExpiresAt]);
                END;
                """);
        }
    }
}
