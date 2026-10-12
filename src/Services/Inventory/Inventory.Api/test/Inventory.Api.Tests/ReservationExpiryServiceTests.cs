using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SneakerShop.Contracts;
using Xunit;

namespace Inventory.Api.Tests;

public sealed class ReservationExpiryServiceTests
{
    [Fact]
    public async Task ProcessExpiredReservationsAsync_ReleasesReservedStockAndPublishesExpiryEvent()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var variantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var publisher = new Mock<IPublishEndpoint>();

        await using (var db = new InventoryDbContext(options))
        {
            db.StockItems.Add(new StockItem
            {
                VariantId = variantId,
                Sku = "ABC-100",
                Quantity = 7,
                Reserved = 3,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
            });

            db.Reservations.Add(new StockReservation
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                VariantId = variantId,
                Quantity = 3,
                Status = ReservationStatus.Reserved,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
            });

            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<InventoryDbContext>(builder => builder.UseInMemoryDatabase(options.Database.GetDbConnection()?.Database ?? "inventory-tests"));
        services.AddSingleton<IPublishEndpoint>(publisher.Object);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<ReservationExpiryService>();
        var service = new ReservationExpiryService(scopeFactory, logger);

        await service.ProcessExpiredReservationsAsync(CancellationToken.None);

        await using (var db = provider.CreateScope().ServiceProvider.GetRequiredService<InventoryDbContext>())
        {
            var reservation = await db.Reservations.SingleAsync(r => r.OrderId == orderId);
            var stock = await db.StockItems.SingleAsync(s => s.VariantId == variantId);

            Assert.Equal(ReservationStatus.Expired, reservation.Status);
            Assert.Equal(0, stock.Reserved);
        }

        publisher.Verify(p => p.Publish(
            It.Is<OrderReservationExpired>(e => e.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
