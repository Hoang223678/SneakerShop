using Catalog.Api.Domain;
using Catalog.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Data;

namespace Catalog.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        await LegacyDatabaseMigrationAdoption.AdoptEnsureCreatedDatabaseAsync(
            db, "Brands", "__EFMigrationsHistory", "20261009183844_InitialCreate");
        await db.Database.MigrateAsync();
        if (await db.Sneakers.AnyAsync()) return;

        var existing = await db.Brands.ToDictionaryAsync(b => b.Name);
        foreach (var name in SeedCatalog.Sneakers.Select(s => s.Brand).Distinct())
        {
            if (existing.ContainsKey(name)) continue;
            var brand = new Brand { Name = name, Slug = Slug.From(name) };
            db.Brands.Add(brand);
            existing[name] = brand;
        }
        await db.SaveChangesAsync();

        foreach (var s in SeedCatalog.Sneakers)
        {
            db.Sneakers.Add(new Sneaker
            {
                Id = s.Id,
                Name = s.Name,
                Slug = Slug.From(s.Name),
                Description = s.Description,
                BrandId = existing[s.Brand].Id,
                Gender = Enum.Parse<Gender>(s.Gender),
                Price = s.Price,
                ImageUrl = s.ImageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var v in SeedCatalog.Variants)
        {
            db.Variants.Add(new SneakerVariant
            {
                Id = v.Id,
                SneakerId = v.SneakerId,
                Sku = v.Sku,
                Size = v.Size,
                Color = v.Color
            });
        }

        await db.SaveChangesAsync();
    }
}
