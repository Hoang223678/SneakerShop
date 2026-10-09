using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using SneakerShop.Contracts;

namespace Ordering.Api.Consumers;

/// <summary>Saga step 2 (happy path): stock is held, so the order is confirmed.</summary>
public class StockReservedConsumer(OrderDbContext db) : IConsumer<StockReserved>
{
    public async Task Consume(ConsumeContext<StockReserved> context)
    {
        var ct = context.CancellationToken;
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == context.Message.OrderId, ct);
        if (order is null) return;

        // The customer cancelled while the reservation was in flight: make sure Inventory lets the stock go.
        if (order.Status == OrderStatus.Cancelled)
        {
            await context.Publish(new OrderCancelled(order.Id), ct);
            return;
        }

        if (order.TryConfirm())
            await db.SaveChangesAsync(ct);
    }
}
