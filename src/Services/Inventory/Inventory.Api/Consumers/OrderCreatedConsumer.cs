using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

/// <summary>
/// Saga step 1: try to reserve stock for every line of a new order (all-or-nothing).
/// Replies with StockReserved or StockReservationFailed.
/// </summary>
public class OrderCreatedConsumer(InventoryDbContext db) : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;
        await OrderProcessingLock.AcquireAsync(db, msg.OrderId, ct);

        if (await db.OrderCancellations.AnyAsync(x => x.OrderId == msg.OrderId, ct))
            return;

        // Idempotency: reservations only exist after a successful attempt, so re-announce success.
        if (await db.Reservations.AnyAsync(r => r.OrderId == msg.OrderId, ct))
        {
            await context.Publish(new StockReserved(msg.OrderId), ct);
            await db.SaveChangesAsync(ct);
            return;
        }

        var wanted = msg.Items
            .GroupBy(i => i.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var variantIds = wanted.Keys.ToList();
        var stocks = await db.StockItems.Where(s => variantIds.Contains(s.VariantId)).ToDictionaryAsync(s => s.VariantId, ct);

        foreach (var (variantId, quantity) in wanted)
        {
            if (!stocks.TryGetValue(variantId, out var stock) || stock.Available < quantity)
            {
                await context.Publish(
                    new StockReservationFailed(msg.OrderId, $"Insufficient stock for variant {variantId}."), ct);
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        var now = DateTime.UtcNow;
        foreach (var (variantId, quantity) in wanted)
        {
            var stock = stocks[variantId];
            stock.Reserved += quantity;
            stock.UpdatedAt = now;

            db.Reservations.Add(new StockReservation
            {
                OrderId = msg.OrderId,
                VariantId = variantId,
                Quantity = quantity,
                Status = ReservationStatus.Reserved,
                CreatedAt = now,
                ExpiresAt = msg.PaymentExpiresAt
            });
        }

        // If two orders race for the last pair, RowVersion makes one of them fail with a concurrency
        // exception; MassTransit retries it and it then sees the updated stock.
        await context.Publish(new StockReserved(msg.OrderId), ct);
        await db.SaveChangesAsync(ct);
    }
}
