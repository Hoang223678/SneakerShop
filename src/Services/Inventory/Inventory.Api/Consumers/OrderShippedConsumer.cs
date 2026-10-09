using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

/// <summary>When an order ships, the pairs physically leave the warehouse: deduct them from stock.</summary>
public class OrderShippedConsumer(InventoryDbContext db) : IConsumer<OrderShipped>
{
    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        var ct = context.CancellationToken;

        var reservations = await db.Reservations
            .Where(r => r.OrderId == context.Message.OrderId && r.Status == ReservationStatus.Reserved)
            .ToListAsync(ct);

        if (reservations.Count == 0) return;

        var variantIds = reservations.Select(r => r.VariantId).Distinct().ToList();
        var stocks = await db.StockItems.Where(s => variantIds.Contains(s.VariantId)).ToDictionaryAsync(s => s.VariantId, ct);

        var now = DateTime.UtcNow;
        foreach (var reservation in reservations)
        {
            if (stocks.TryGetValue(reservation.VariantId, out var stock))
            {
                stock.Quantity = Math.Max(0, stock.Quantity - reservation.Quantity);
                stock.Reserved = Math.Max(0, stock.Reserved - reservation.Quantity);
                stock.UpdatedAt = now;
            }

            reservation.Status = ReservationStatus.Committed;
            reservation.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
