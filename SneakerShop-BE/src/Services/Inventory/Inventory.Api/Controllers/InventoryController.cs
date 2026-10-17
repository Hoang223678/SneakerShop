using Inventory.Api.Caching;
using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using ZiggyCreatures.Caching.Fusion;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController(InventoryDbContext db, IFusionCache cache) : ControllerBase
{
    /// <summary>Public: how many pairs of one variant can be bought right now (may be up to 5 seconds old).</summary>
    [HttpGet("{variantId:guid}/availability")]
    public async Task<ActionResult<StockAvailability>> GetAvailability(Guid variantId, CancellationToken ct)
    {
        var availability = await cache.GetOrSetAsync<StockAvailability?>(
            InventoryCacheKeys.Availability(variantId),
            async token => (await LoadAvailabilityAsync([variantId], token)).GetValueOrDefault(variantId),
            InventoryCacheKeys.AvailabilityDuration,
            ct);
        return availability is null ? NotFound() : availability;
    }

    /// <summary>Public: availability for many variants at once (e.g. all sizes on a product page).</summary>
    [HttpPost("availability")]
    public async Task<ActionResult<List<StockAvailability>>> GetAvailabilityBatch(AvailabilityRequest request, CancellationToken ct)
    {
        var ids = request.VariantIds.Distinct().Take(200).ToList();
        var result = new List<StockAvailability>(ids.Count);
        var misses = new List<Guid>();

        foreach (var id in ids)
        {
            var cached = await cache.TryGetAsync<StockAvailability?>(InventoryCacheKeys.Availability(id), token: ct);
            if (!cached.HasValue) misses.Add(id);
            else if (cached.Value is not null) result.Add(cached.Value);
        }

        if (misses.Count > 0)
        {
            var loaded = await LoadAvailabilityAsync(misses, ct);
            foreach (var id in misses)
            {
                var availability = loaded.GetValueOrDefault(id);
                await cache.SetAsync(InventoryCacheKeys.Availability(id), availability, InventoryCacheKeys.AvailabilityDuration, ct);
                if (availability is not null) result.Add(availability);
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, StockAvailability>> LoadAvailabilityAsync(List<Guid> variantIds, CancellationToken ct)
    {
        var items = await db.StockItems.AsNoTracking().Where(s => variantIds.Contains(s.VariantId)).ToListAsync(ct);
        return items.ToDictionary(
            i => i.VariantId,
            i => new StockAvailability(i.VariantId, Math.Max(0, i.Available), i.Available > 0));
    }

    /// <summary>Admin: stock overview. Use maxAvailable to list low-stock variants.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockItemDto>>> List(
        [FromQuery] int? maxAvailable, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.StockItems.AsNoTracking().AsQueryable();
        if (maxAvailable.HasValue)
            query = query.Where(s => s.Quantity - s.Reserved <= maxAvailable.Value);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(s => s.Sku).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(s => new StockItemDto(s.VariantId, s.Sku, s.Quantity, s.Reserved, s.Available, s.UpdatedAt)).ToList();
        return new PagedResult<StockItemDto>(items, page, pageSize, total);
    }

    /// <summary>Admin: set the physical quantity on hand (e.g. after receiving a delivery or a stock count).</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{variantId:guid}")]
    public async Task<ActionResult<StockItemDto>> SetStock(Guid variantId, SetStockRequest request, CancellationToken ct)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.VariantId == variantId, ct);
        if (item is null) return NotFound();

        if (request.Quantity < item.Reserved)
            throw new AppException(StatusCodes.Status409Conflict,
                $"Quantity cannot be lower than the {item.Reserved} pair(s) already reserved for open orders.");

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(StatusCodes.Status409Conflict, "Stock was changed by another request. Please retry.");
        }
        await cache.RemoveAsync(InventoryCacheKeys.Availability(variantId));

        return new StockItemDto(item.VariantId, item.Sku, item.Quantity, item.Reserved, item.Available, item.UpdatedAt);
    }
}
