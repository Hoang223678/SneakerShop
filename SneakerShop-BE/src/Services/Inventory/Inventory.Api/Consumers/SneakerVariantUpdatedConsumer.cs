using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

public class SneakerVariantUpdatedConsumer(InventoryDbContext db) : IConsumer<SneakerVariantUpdated>
{
    public async Task Consume(ConsumeContext<SneakerVariantUpdated> context)
    {
        var msg = context.Message;
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.VariantId == msg.VariantId, context.CancellationToken);
        if (item is null)
        {
            db.StockItems.Add(new StockItem
            {
                VariantId = msg.VariantId,
                SneakerId = msg.SneakerId,
                Sku = msg.Sku,
                Quantity = 0,
                Reserved = 0,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            item.SneakerId = msg.SneakerId;
            item.Sku = msg.Sku;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
