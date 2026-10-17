using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Data;

namespace Inventory.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await LegacyDatabaseMigrationAdoption.AdoptEnsureCreatedDatabaseAsync(
            db, "StockItems", "__EFMigrationsHistory", "20261009183837_InitialCreate");
        await db.Database.MigrateAsync();
        if (await db.StockItems.AnyAsync()) return;

        // Demo stock for the seeded catalog variants (same IDs as Catalog's seed).
        foreach (var v in SeedCatalog.Variants)
        {
            db.StockItems.Add(new StockItem
            {
                VariantId = v.Id,
                Sku = v.Sku,
                Quantity = SeedCatalog.InitialStockPerVariant,
                Reserved = 0,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
