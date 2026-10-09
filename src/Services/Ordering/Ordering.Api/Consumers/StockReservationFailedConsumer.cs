using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using SneakerShop.Contracts;

namespace Ordering.Api.Consumers;

/// <summary>Saga step 2 (failure path): not enough stock, so the order is rejected.</summary>
public class StockReservationFailedConsumer(OrderDbContext db) : IConsumer<StockReservationFailed>
{
    public async Task Consume(ConsumeContext<StockReservationFailed> context)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == context.Message.OrderId, context.CancellationToken);
        if (order is null) return;

        if (order.TryReject(context.Message.Reason))
            await db.SaveChangesAsync(context.CancellationToken);
    }
}
