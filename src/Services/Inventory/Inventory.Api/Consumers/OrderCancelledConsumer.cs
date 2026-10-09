using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

/// <summary>Compensation: give reserved pairs back when an order is cancelled.</summary>
public class OrderCancelledConsumer(InventoryDbContext db) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var ct = context.CancellationToken;

        var reservations = await db.Reservations
            .Where(r => r.OrderId == context.Message.OrderId && r.Status == ReservationStatus.Reserved)
            .ToListAsync(ct);

        if (reservations.Count == 0) return; // nothing to release (or already released)

        var variantIds = reservations.Select(r => r.VariantId).Distinct().ToList();
        var stocks = await db.StockItems.Where(s => variantIds.Contains(s.VariantId)).ToDictionaryAsync(s => s.VariantId, ct);

        var now = DateTime.UtcNow;
        foreach (var reservation in reservations)
        {
            if (stocks.TryGetValue(reservation.VariantId, out var stock))
            {
                stock.Reserved = Math.Max(0, stock.Reserved - reservation.Quantity);
                stock.UpdatedAt = now;
            }

            reservation.Status = ReservationStatus.Released;
            reservation.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
