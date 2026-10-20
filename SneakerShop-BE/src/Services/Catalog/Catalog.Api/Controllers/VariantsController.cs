using Catalog.Api.Caching;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.Dtos;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/variants")]
public class VariantsController(CatalogDbContext db, IPublishEndpoint publisher, IFusionCache cache) : ControllerBase
{
    /// <summary>Catalog variants with their sneaker: one sneakerId maps to many variantIds (sizes/colors).</summary>
    [HttpGet]
    public async Task<ActionResult<List<VariantListItem>>> List([FromQuery] Guid? sneakerId, CancellationToken ct)
    {
        var query = db.Variants.AsNoTracking().Where(v => v.Sneaker.IsActive);
        if (sneakerId.HasValue)
            query = query.Where(v => v.SneakerId == sneakerId.Value);

        return await query
            .OrderBy(v => v.Sneaker.Name)
            .ThenBy(v => v.Size)
            .ThenBy(v => v.Color)
            .Select(v => new VariantListItem(v.Id, v.SneakerId, v.Sneaker.Name, v.Sku, v.Size, v.Color))
            .ToListAsync(ct);
    }

    /// <summary>Create a size/color variant for one sneaker. Inventory creates empty stock from the published event.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{sneakerId:guid}")]
    public async Task<ActionResult<VariantDto>> Create(Guid sneakerId, CreateVariantRequest request, CancellationToken ct)
    {
        if (!await db.Sneakers.AnyAsync(s => s.Id == sneakerId, ct)) return NotFound();

        var sku = request.Sku.Trim().ToUpperInvariant();
        var color = request.Color.Trim();
        await EnsureUniqueAsync(sneakerId, sku, request.Size, color, exceptId: null, ct);

        var variant = new SneakerVariant { SneakerId = sneakerId, Sku = sku, Size = request.Size, Color = color };
        db.Variants.Add(variant);
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(sneakerId);
        await publisher.Publish(new SneakerVariantCreated(variant.Id, sneakerId, variant.Sku), ct);

        return Created($"/api/variants/{variant.Id}", new VariantDto(variant.Id, sneakerId, variant.Sku, variant.Size, variant.Color));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VariantDto>> Update(Guid id, CreateVariantRequest request, CancellationToken ct)
    {
        var variant = await db.Variants.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variant is null) return NotFound();

        var sku = request.Sku.Trim().ToUpperInvariant();
        var color = request.Color.Trim();
        await EnsureUniqueAsync(variant.SneakerId, sku, request.Size, color, exceptId: id, ct);

        variant.Sku = sku;
        variant.Size = request.Size;
        variant.Color = color;
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(variant.SneakerId);
        await publisher.Publish(new SneakerVariantUpdated(variant.Id, variant.SneakerId, variant.Sku), ct);

        return new VariantDto(variant.Id, variant.SneakerId, variant.Sku, variant.Size, variant.Color);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var variant = await db.Variants.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variant is null) return NotFound();

        var sneakerId = variant.SneakerId;
        db.Variants.Remove(variant);
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(sneakerId);
        await publisher.Publish(new SneakerVariantDeleted(id), ct);
        return NoContent();
    }

    private async Task EnsureUniqueAsync(
        Guid sneakerId, string sku, decimal size, string color, Guid? exceptId, CancellationToken ct)
    {
        var skuTaken = exceptId is Guid skuId
            ? await db.Variants.AnyAsync(v => v.Sku == sku && v.Id != skuId, ct)
            : await db.Variants.AnyAsync(v => v.Sku == sku, ct);
        if (skuTaken)
            throw new AppException(StatusCodes.Status409Conflict, "SKU already exists.");

        var pairTaken = exceptId is Guid pairId
            ? await db.Variants.AnyAsync(v =>
                v.SneakerId == sneakerId && v.Size == size && v.Color == color && v.Id != pairId, ct)
            : await db.Variants.AnyAsync(v =>
                v.SneakerId == sneakerId && v.Size == size && v.Color == color, ct);
        if (pairTaken)
            throw new AppException(StatusCodes.Status409Conflict, "This size and color already exist for the sneaker.");
    }

    private async Task InvalidateAsync(Guid sneakerId)
    {
        await cache.RemoveAsync(CatalogCacheKeys.Sneaker(sneakerId));
        await cache.RemoveByTagAsync(CatalogCacheKeys.SneakerListTag);
    }
}
