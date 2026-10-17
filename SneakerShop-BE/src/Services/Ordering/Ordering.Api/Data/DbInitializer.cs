using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks.Data;

namespace Ordering.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrderDbContext>>();
        var sagaDbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<Ordering.Api.Sagas.OrderSagaDbContext>>();
        await using var orderDb = await dbFactory.CreateDbContextAsync();
        await using var sagaDb = await sagaDbFactory.CreateDbContextAsync();
        await LegacyDatabaseMigrationAdoption.AdoptEnsureCreatedDatabaseAsync(
            orderDb, "Orders", "__EFMigrationsHistory_Ordering", "20261009183819_InitialCreate");
        await orderDb.Database.MigrateAsync();
        await sagaDb.Database.MigrateAsync();
    }
}
