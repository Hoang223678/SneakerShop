using Microsoft.EntityFrameworkCore;

namespace Ordering.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.EnsureCreatedAsync(); // demo convenience - use EF migrations in production
    }
}
