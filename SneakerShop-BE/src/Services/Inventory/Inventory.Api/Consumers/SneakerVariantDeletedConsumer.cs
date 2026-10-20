using Inventory.Api.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Inventory.Api.Consumers;

public class SneakerVariantDeletedConsumer(InventoryDbContext db) : IConsumer<SneakerVariantDeleted>
{
    public async Task Consume(ConsumeContext<SneakerVariantDeleted> context)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(
            s => s.VariantId == context.Message.VariantId, context.CancellationToken);
        if (item is null || item.Reserved > 0)
            return;

        db.StockItems.Remove(item);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
