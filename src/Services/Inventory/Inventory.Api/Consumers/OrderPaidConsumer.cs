using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

public class OrderPaidConsumer(InventoryDbContext db) : IConsumer<OrderPaid>
{
    public async Task Consume(ConsumeContext<OrderPaid> context)
    {
        var reservations = await db.Reservations
            .Where(r => r.OrderId == context.Message.OrderId && r.Status == ReservationStatus.Reserved)
            .ToListAsync(context.CancellationToken);

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Paid;
            reservation.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
