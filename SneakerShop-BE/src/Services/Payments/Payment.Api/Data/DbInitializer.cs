using Microsoft.EntityFrameworkCore;

namespace Payment.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<PaymentDbContext>>()
            .CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }
}
