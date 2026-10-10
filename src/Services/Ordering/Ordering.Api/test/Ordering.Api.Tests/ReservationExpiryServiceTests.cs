using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Services;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SneakerShop.Contracts;

namespace Ordering.Api.Tests;

public class ReservationExpiryServiceTests
{
    [Fact]
    public async Task Expired_unpaid_reservations_release_stock_and_publish_expiration()
    {
        await using var provider = CreateProvider();
        var variantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            db.StockItems.Add(new StockItem { VariantId = variantId, Quantity = 5, Reserved = 2 });
            db.Reservations.Add(new StockReservation
            {
                OrderId = orderId,
                VariantId = variantId,
                Quantity = 2,
                Status = ReservationStatus.Reserved,
                CreatedAt = DateTime.UtcNow.AddMinutes(-16),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var service = new ReservationExpiryService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReservationExpiryService>.Instance);
        await service.ProcessExpiredReservationsAsync(CancellationToken.None);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stock = await db.StockItems.SingleAsync(x => x.VariantId == variantId);
            var reservation = await db.Reservations.SingleAsync(x => x.OrderId == orderId);
            Assert.Equal(0, stock.Reserved);
            Assert.Equal(ReservationStatus.Expired, reservation.Status);
        }

        Assert.True(await harness.Published.Any<OrderReservationExpired>());
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddMassTransitTestHarness(x =>
            x.UsingInMemory((_, _) => { }));
        return services.BuildServiceProvider();
    }
}
