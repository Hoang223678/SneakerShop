using Catalog.Api.Data;
using Catalog.Api.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.Contracts;

namespace Catalog.Api.Consumers;

/// <summary>
/// Counts shipped pairs per sneaker for the best-seller ranking.
/// The class name sets the queue name, which must not collide with Inventory's OrderShippedConsumer.
/// </summary>
public class BestSellerRankingConsumer(CatalogDbContext db, IBestSellerRanking ranking) : IConsumer<OrderShipped>
{
    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        var lines = context.Message.Items;
        var variantIds = lines.Select(l => l.VariantId).Distinct().ToList();
        var sneakerByVariant = await db.Variants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.SneakerId, context.CancellationToken);

        var pairsBySneaker = lines
            .Where(l => sneakerByVariant.ContainsKey(l.VariantId))
            .GroupBy(l => sneakerByVariant[l.VariantId])
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        if (pairsBySneaker.Count == 0) return;

        await ranking.RecordSaleAsync(context.Message.OrderId, pairsBySneaker, DateTime.UtcNow);
    }
}
