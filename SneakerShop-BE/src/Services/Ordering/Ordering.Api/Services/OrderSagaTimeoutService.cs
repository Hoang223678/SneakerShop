using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Sagas;
using SneakerShop.Contracts;

namespace Ordering.Api.Services;

public sealed class OrderSagaTimeoutService(
    IDbContextFactory<OrderSagaDbContext> dbFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderSagaTimeoutService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                await PublishExpiredTimeoutsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to publish order reservation timeout events.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishExpiredTimeoutsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var outboxDb = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTime.UtcNow;

        var due = await db.Set<OrderState>().AsNoTracking()
            .Where(x => x.CurrentState == nameof(OrderStateMachine.AwaitingReservation) &&
                        x.ReservationDeadline <= now)
            .Select(x => x.CorrelationId)
            .ToListAsync(ct);

        foreach (var orderId in due)
            await publisher.Publish(new OrderReservationTimedOut(orderId), ct);

        if (due.Count > 0)
            await outboxDb.SaveChangesAsync(ct);
    }
}
