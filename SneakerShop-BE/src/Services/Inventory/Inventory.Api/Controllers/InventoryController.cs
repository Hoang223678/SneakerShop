using Inventory.Api.Caching;
using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Dtos;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using ZiggyCreatures.Caching.Fusion;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController(InventoryDbContext db, IFusionCache cache, ICatalogVariantClient catalog) : ControllerBase
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

    /// <summary>Admin: stock overview. Filter by sneaker (one sneaker has many size variants) or low stock.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockItemDto>>> List(
        [FromQuery] Guid? sneakerId, [FromQuery] int? maxAvailable, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.StockItems.AsNoTracking().AsQueryable();
        if (sneakerId.HasValue)
            query = query.Where(s => s.SneakerId == sneakerId.Value);
        if (maxAvailable.HasValue)
            query = query.Where(s => s.Quantity - s.Reserved <= maxAvailable.Value);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(s => s.SneakerId).ThenBy(s => s.Sku).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(ToDto).ToList();
        return new PagedResult<StockItemDto>(items, page, pageSize, total);
    }

    /// <summary>Admin: all size/color stock rows for one catalog sneaker.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpGet("sneaker/{sneakerId:guid}")]
    public Task<ActionResult<PagedResult<StockItemDto>>> ListBySneaker(
        Guid sneakerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        List(sneakerId, maxAvailable: null, page, pageSize, ct);

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

        return ToDto(item);
    }

    /// <summary>Admin: create a Catalog size/color variant and a stock row for that sneaker.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{sneakerId:guid}")]
    public async Task<ActionResult<VariantStockDto>> CreateVariant(
        Guid sneakerId, CreateVariantStockRequest request, CancellationToken ct)
    {
        var variant = await catalog.CreateAsync(sneakerId, request.Sku, request.Size, request.Color, ct);
        var item = await UpsertStockAsync(variant.Id, sneakerId, variant.Sku, request.Quantity, ct);
        return Created($"/api/inventory/{item.VariantId}/availability",
            ToVariantDto(item, variant.Size, variant.Color));
    }

    /// <summary>Admin: update SKU, size and color of a variant.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPut("variants/{variantId:guid}")]
    public async Task<ActionResult<VariantStockDto>> UpdateVariant(
        Guid variantId, UpdateVariantRequest request, CancellationToken ct)
    {
        var variant = await catalog.UpdateAsync(variantId, request.Sku, request.Size, request.Color, ct);
        var existing = await db.StockItems.FirstOrDefaultAsync(s => s.VariantId == variantId, ct);
        var quantity = existing?.Quantity ?? 0;
        var item = await UpsertStockAsync(variant.Id, variant.SneakerId, variant.Sku, quantity, ct);
        return ToVariantDto(item, variant.Size, variant.Color);
    }

    /// <summary>Admin: delete a variant from Catalog and its stock row.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("variants/{variantId:guid}")]
    public async Task<IActionResult> DeleteVariant(Guid variantId, CancellationToken ct)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.VariantId == variantId, ct);
        if (item is { Reserved: > 0 })
            throw new AppException(StatusCodes.Status409Conflict,
                $"Cannot delete a variant with {item.Reserved} reserved pair(s).");

        try
        {
            await catalog.DeleteAsync(variantId, ct);
        }
        catch (AppException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
        {
            if (item is null) throw;
        }

        if (item is not null)
        {
            db.StockItems.Remove(item);
            await db.SaveChangesAsync(ct);
            await cache.RemoveAsync(InventoryCacheKeys.Availability(variantId));
        }

        return NoContent();
    }

    private async Task<StockItem> UpsertStockAsync(
        Guid variantId, Guid sneakerId, string sku, int quantity, CancellationToken ct)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.VariantId == variantId, ct);
        if (item is null)
        {
            item = new StockItem
            {
                VariantId = variantId,
                SneakerId = sneakerId,
                Sku = sku,
                Quantity = quantity,
                Reserved = 0,
                UpdatedAt = DateTime.UtcNow
            };
            db.StockItems.Add(item);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.Entry(item).State = EntityState.Detached;
                item = await db.StockItems.SingleAsync(s => s.VariantId == variantId, ct);
                ApplyStock(item, sneakerId, sku, quantity);
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            ApplyStock(item, sneakerId, sku, quantity);
            await db.SaveChangesAsync(ct);
        }

        await cache.RemoveAsync(InventoryCacheKeys.Availability(variantId));
        return item;
    }

    private static void ApplyStock(StockItem item, Guid sneakerId, string sku, int quantity)
    {
        if (quantity < item.Reserved)
            throw new AppException(StatusCodes.Status409Conflict,
                $"Quantity cannot be lower than the {item.Reserved} pair(s) already reserved for open orders.");
        item.SneakerId = sneakerId == Guid.Empty ? item.SneakerId : sneakerId;
        item.Sku = sku;
        item.Quantity = quantity;
        item.UpdatedAt = DateTime.UtcNow;
    }

    private static StockItemDto ToDto(StockItem item) =>
        new(item.VariantId, item.SneakerId, item.Sku, item.Quantity, item.Reserved, item.Available, item.UpdatedAt);

    private static VariantStockDto ToVariantDto(StockItem item, decimal size, string color) =>
        new(item.VariantId, item.SneakerId, item.Sku ?? string.Empty, size, color,
            item.Quantity, item.Reserved, item.Available, item.UpdatedAt);
}
