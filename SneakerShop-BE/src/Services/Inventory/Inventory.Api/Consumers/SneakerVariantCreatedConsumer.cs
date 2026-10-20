using Inventory.Api.Data;
using Inventory.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

/// <summary>When Catalog adds a variant, create an empty stock record for it.</summary>
public class SneakerVariantCreatedConsumer(InventoryDbContext db) : IConsumer<SneakerVariantCreated>
{
    public async Task Consume(ConsumeContext<SneakerVariantCreated> context)
    {
        var msg = context.Message;

        if (await db.StockItems.AnyAsync(s => s.VariantId == msg.VariantId, context.CancellationToken))
            return; // idempotent: message may be delivered more than once

        db.StockItems.Add(new StockItem
        {
            VariantId = msg.VariantId,
            SneakerId = msg.SneakerId,
            Sku = msg.Sku,
            Quantity = 0,
            Reserved = 0,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
