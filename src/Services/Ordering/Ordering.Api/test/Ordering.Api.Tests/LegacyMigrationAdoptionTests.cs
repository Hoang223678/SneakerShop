using Inventory.Api.Data;
using Inventory.Api.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using SneakerShop.BuildingBlocks.Data;
using Testcontainers.MsSql;

namespace Ordering.Api.Tests;

public class LegacyMigrationAdoptionTests
{
    [Fact]
    public async Task Ensure_created_ordering_and_inventory_databases_are_adopted_and_upgraded()
    {
        await using var sqlServer = new MsSqlBuilder()
            .WithPassword("Sneaker_Test123!")
            .Build();
        await sqlServer.StartAsync();

        var rootConnectionString = sqlServer.GetConnectionString();
        var orderingConnectionString = DatabaseConnection(rootConnectionString, "LegacyOrdering");
        var inventoryConnectionString = DatabaseConnection(rootConnectionString, "LegacyInventory");
        await using (var connection = new SqlConnection(DatabaseConnection(rootConnectionString, "master")))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE DATABASE [LegacyOrdering]; CREATE DATABASE [LegacyInventory];";
            await command.ExecuteNonQueryAsync();
        }

        await using (var orders = CreateOrderDb(orderingConnectionString))
        {
            await orders.Database.ExecuteSqlRawAsync("""
                CREATE TABLE [Orders] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [UserId] uniqueidentifier NOT NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [TotalAmount] decimal(18,2) NOT NULL,
                    [ShippingName] nvarchar(150) NOT NULL,
                    [ShippingPhone] nvarchar(30) NOT NULL,
                    [ShippingAddress] nvarchar(500) NOT NULL,
                    [FailureReason] nvarchar(500) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    [RowVersion] rowversion NOT NULL
                );
                CREATE TABLE [OrderItems] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrderId] uniqueidentifier NOT NULL,
                    [VariantId] uniqueidentifier NOT NULL,
                    [SneakerId] uniqueidentifier NOT NULL,
                    [SneakerName] nvarchar(200) NOT NULL,
                    [Sku] nvarchar(64) NOT NULL,
                    [Size] decimal(4,1) NOT NULL,
                    [Color] nvarchar(50) NOT NULL,
                    [UnitPrice] decimal(18,2) NOT NULL,
                    [Quantity] int NOT NULL,
                    CONSTRAINT [FK_OrderItems_Orders_OrderId]
                        FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
                """);

            var userId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await orders.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [Orders]
                    ([Id], [UserId], [Status], [TotalAmount], [ShippingName], [ShippingPhone],
                     [ShippingAddress], [FailureReason], [CreatedAt], [UpdatedAt])
                VALUES
                    ({orderId}, {userId}, N'Pending', 25.00, N'Legacy', N'0900000000',
                     N'Legacy address', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());
                """);

            await LegacyDatabaseMigrationAdoption.AdoptEnsureCreatedDatabaseAsync(
                orders, "Orders", "__EFMigrationsHistory_Ordering", "20261009183819_InitialCreate");
            await orders.Database.MigrateAsync();

            var migrated = await orders.Orders.SingleAsync(x => x.Id == orderId);
            Assert.Equal(userId, migrated.UserId);
            Assert.False(migrated.IsPaid);
            Assert.StartsWith("legacy-", migrated.IdempotencyKey);
            Assert.Equal(0, await orders.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM [dbo].[OutboxMessage]").SingleAsync());
        }

        await using (var inventory = CreateInventoryDb(inventoryConnectionString))
        {
            await inventory.Database.ExecuteSqlRawAsync("""
                CREATE TABLE [StockItems] (
                    [VariantId] uniqueidentifier NOT NULL PRIMARY KEY,
                    [Sku] nvarchar(64) NULL,
                    [Quantity] int NOT NULL,
                    [Reserved] int NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    [RowVersion] rowversion NOT NULL
                );
                CREATE TABLE [StockReservations] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrderId] uniqueidentifier NOT NULL,
                    [VariantId] uniqueidentifier NOT NULL,
                    [Quantity] int NOT NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL
                );
                CREATE INDEX [IX_StockReservations_OrderId] ON [StockReservations] ([OrderId]);
                CREATE INDEX [IX_StockReservations_VariantId] ON [StockReservations] ([VariantId]);
                """);

            var variantId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var reservationId = Guid.NewGuid();
            await inventory.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [StockItems] ([VariantId], [Sku], [Quantity], [Reserved], [UpdatedAt])
                VALUES ({variantId}, N'LEGACY-1', 4, 1, SYSUTCDATETIME());
                INSERT INTO [StockReservations]
                    ([Id], [OrderId], [VariantId], [Quantity], [Status], [CreatedAt])
                VALUES
                    ({reservationId}, {orderId}, {variantId}, 1, N'Reserved', DATEADD(minute, -2, SYSUTCDATETIME()));
                """);

            await LegacyDatabaseMigrationAdoption.AdoptEnsureCreatedDatabaseAsync(
                inventory, "StockItems", "__EFMigrationsHistory", "20261009183837_InitialCreate");
            await inventory.Database.MigrateAsync();

            var stock = await inventory.StockItems.SingleAsync(x => x.VariantId == variantId);
            var reservation = await inventory.Reservations.SingleAsync(x => x.Id == reservationId);
            Assert.Equal(4, stock.Quantity);
            Assert.Equal(1, stock.Reserved);
            Assert.Equal(ReservationStatus.Reserved, reservation.Status);
            Assert.InRange(reservation.ExpiresAt - reservation.CreatedAt,
                TimeSpan.FromMinutes(14.9), TimeSpan.FromMinutes(15.1));
            Assert.Equal(0, await inventory.OrderCancellations.CountAsync());
        }
    }

    private static OrderDbContext CreateOrderDb(string connectionString) =>
        new(new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(connectionString, options =>
                options.MigrationsHistoryTable("__EFMigrationsHistory_Ordering"))
            .Options);

    private static InventoryDbContext CreateInventoryDb(string connectionString) =>
        new(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString)
            .Options);

    private static string DatabaseConnection(string connectionString, string databaseName) =>
        new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
}
