using Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Data;

namespace Identity.Api.Data;

public static class DbInitializer
{
    /// <summary>
    /// Creates the schema (demo convenience - use EF migrations in production) and seeds an admin and a demo customer.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        await LegacyDatabaseMigrationAdoption.AdoptEnsureCreatedDatabaseAsync(
            db, "Users", "__EFMigrationsHistory", "20261009183851_InitialCreate");
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync()) return;

        db.Users.Add(new User
        {
            Email = (config["Seed:AdminEmail"] ?? "admin@sneakershop.local").ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(config["Seed:AdminPassword"] ?? "Admin@12345"),
            FullName = "Shop Admin",
            Role = Roles.Admin
        });
        db.Users.Add(new User
        {
            Email = (config["Seed:CustomerEmail"] ?? "customer@sneakershop.local").ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(config["Seed:CustomerPassword"] ?? "Customer@12345"),
            FullName = "Demo Customer",
            Phone = "0900000000",
            Role = Roles.Customer
        });
        await db.SaveChangesAsync();
    }
}
