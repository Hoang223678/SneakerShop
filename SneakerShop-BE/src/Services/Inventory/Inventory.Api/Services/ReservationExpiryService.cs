using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Services;

public sealed class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await ProcessExpiredReservationsAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(exception, "A reservation changed while expiration was being processed; it will be retried.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task ProcessExpiredReservationsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTime.UtcNow;
        var expired = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        var variantIds = expired.Select(r => r.VariantId).Distinct().ToList();
        var stocks = await db.StockItems.Where(s => variantIds.Contains(s.VariantId))
            .ToDictionaryAsync(s => s.VariantId, ct);

        foreach (var reservation in expired)
        {
            if (stocks.TryGetValue(reservation.VariantId, out var stock))
            {
                stock.Reserved = Math.Max(0, stock.Reserved - reservation.Quantity);
                stock.UpdatedAt = now;
            }

            reservation.Status = ReservationStatus.Expired;
            reservation.UpdatedAt = now;
        }

        foreach (var orderId in expired.Select(r => r.OrderId).Distinct())
            await publisher.Publish(new OrderReservationExpired(orderId), ct);

        await db.SaveChangesAsync(ct);
    }
}
