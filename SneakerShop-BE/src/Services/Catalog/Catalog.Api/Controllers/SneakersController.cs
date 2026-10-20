using System.Globalization;
using Catalog.Api.Caching;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.Dtos;
using Catalog.Api.Helpers;
using Catalog.Api.Interfaces;
using Catalog.Api.Services;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/sneakers")]
public class SneakersController(
    CatalogDbContext db, IPublishEndpoint publisher, IFusionCache cache, IBestSellerRanking ranking) : ControllerBase, ISneakersController
{
    /// <summary>Search and filter the catalog (active products only).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SneakerListItem>>> List([FromQuery] SneakerQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        return await cache.GetOrSetAsync<PagedResult<SneakerListItem>>(
            CatalogCacheKeys.SneakerList(q, page, pageSize),
            token => QueryListAsync(q, page, pageSize, token),
            CatalogCacheKeys.SneakerListDuration,
            [CatalogCacheKeys.SneakerListTag],
            ct);
    }

    /// <summary>Best-selling active sneakers over the last <paramref name="days"/> days (1-30), counted from shipped orders.</summary>
    [HttpGet("best-sellers")]
    public async Task<ActionResult<List<SneakerListItem>>> BestSellers(
        [FromQuery] int days = 7, [FromQuery] int take = 10, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, RedisBestSellerRanking.MaxDays);
        take = Math.Clamp(take, 1, 50);

        return await cache.GetOrSetAsync<List<SneakerListItem>>(
            CatalogCacheKeys.BestSellers(days, take),
            async token =>
            {
                // Ask for extra ids because inactive sneakers are dropped below.
                var ids = await ranking.TopAsync(days, take * 2);
                var items = await db.Sneakers.AsNoTracking()
                    .Where(s => s.IsActive && ids.Contains(s.Id))
                    .Select(s => new SneakerListItem(s.Id, s.Name, s.Slug, s.Brand.Name, s.Gender, s.Price, s.ImageUrl))
                    .ToDictionaryAsync(s => s.Id, token);
                return ids.Where(items.ContainsKey).Select(id => items[id]).Take(take).ToList();
            },
            CatalogCacheKeys.BestSellersDuration,
            [CatalogCacheKeys.SneakerListTag],
            ct);
    }

    private async Task<PagedResult<SneakerListItem>> QueryListAsync(
        SneakerQuery q, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Sneakers.AsNoTracking().Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(s => s.Name.Contains(term));
        }
        if (q.BrandId.HasValue) query = query.Where(s => s.BrandId == q.BrandId.Value);
        if (q.Gender.HasValue) query = query.Where(s => s.Gender == q.Gender.Value);
        if (q.MinPrice.HasValue) query = query.Where(s => s.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue) query = query.Where(s => s.Price <= q.MaxPrice.Value);

        query = q.Sort switch
        {
            "price_asc" => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "newest" => query.OrderByDescending(s => s.CreatedAt),
            _ => query.OrderBy(s => s.Name)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SneakerListItem(s.Id, s.Name, s.Slug, s.Brand.Name, s.Gender, s.Price, s.ImageUrl))
            .ToListAsync(ct);

        return new PagedResult<SneakerListItem>(items, page, pageSize, total);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SneakerDetail>> Get(Guid id, CancellationToken ct)
    {
        var sneaker = await cache.GetOrSetAsync<SneakerDetail?>(
            CatalogCacheKeys.Sneaker(id),
            async token =>
            {
                var entity = await db.Sneakers.AsNoTracking()
                    .Include(s => s.Brand)
                    .Include(s => s.Variants)
                    .FirstOrDefaultAsync(s => s.Id == id, token);
                return entity is null ? null : ToDetail(entity);
            },
            CatalogCacheKeys.SneakerDetailDuration,
            ct);

        // Inactive (soft-deleted) products are only visible to admins.
        if (sneaker is null || (!sneaker.IsActive && !User.IsAdmin()))
            return NotFound();

        return sneaker;
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<SneakerDetail>> Create(SneakerUpsertRequest request, CancellationToken ct)
    {
        var slug = Slug.From(request.Name);
        if (slug.Length == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "Name must contain letters or digits.");
        if (!await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct))
            throw new AppException(StatusCodes.Status400BadRequest, "Brand does not exist.");
        if (await db.Sneakers.AnyAsync(s => s.Slug == slug, ct))
            throw new AppException(StatusCodes.Status409Conflict, "A sneaker with this name already exists.");

        var sneaker = new Sneaker
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description,
            BrandId = request.BrandId,
            Gender = request.Gender,
            Price = request.Price,
            ImageUrl = request.ImageUrl
        };

        db.Sneakers.Add(sneaker);
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(sneaker.Id);

        var created = await db.Sneakers.AsNoTracking().Include(s => s.Brand).Include(s => s.Variants)
            .FirstAsync(s => s.Id == sneaker.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = sneaker.Id }, ToDetail(created));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SneakerDetail>> Update(Guid id, SneakerUpsertRequest request, CancellationToken ct)
    {
        var sneaker = await db.Sneakers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sneaker is null) return NotFound();

        var slug = Slug.From(request.Name);
        if (slug.Length == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "Name must contain letters or digits.");
        if (await db.Sneakers.AnyAsync(s => s.Slug == slug && s.Id != id, ct))
            throw new AppException(StatusCodes.Status409Conflict, "A sneaker with this name already exists.");
        if (!await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct))
            throw new AppException(StatusCodes.Status400BadRequest, "Brand does not exist.");

        sneaker.Name = request.Name.Trim();
        sneaker.Slug = slug;
        sneaker.Description = request.Description;
        sneaker.BrandId = request.BrandId;
        sneaker.Gender = request.Gender;
        sneaker.Price = request.Price;
        sneaker.ImageUrl = request.ImageUrl;
        sneaker.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id);

        var updated = await db.Sneakers.AsNoTracking().Include(s => s.Brand).Include(s => s.Variants)
            .FirstAsync(s => s.Id == id, ct);
        return ToDetail(updated);
    }

    /// <summary>Soft delete: the product disappears from the storefront but old orders keep their snapshot.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sneaker = await db.Sneakers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sneaker is null) return NotFound();

        sneaker.IsActive = false;
        sneaker.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Add one color to every active sneaker and return each sneaker's variant ids for that color.
    /// Sizes follow the ones the sneaker already sells; a sneaker with no variants gets sizes 8, 9, 10 and 11.
    /// Existing size/color pairs are reused. Each new variant publishes <see cref="SneakerVariantCreated"/>
    /// so Inventory can create an empty stock record.
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("variants")]
    public async Task<ActionResult<List<SneakerColorVariantsDto>>> CreateByColor(
        CreateColorVariantsRequest request, CancellationToken ct)
    {
        var color = request.Color.Trim();
        if (color.Length == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "Color is required.");

        var sneakers = await db.Sneakers
            .Include(s => s.Variants)
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var usedSkus = (await db.Variants.Select(v => v.Sku).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<SneakerVariant>();
        var results = new List<SneakerColorVariantsDto>(sneakers.Count);

        foreach (var sneaker in sneakers)
        {
            var sizes = sneaker.Variants.Select(v => v.Size).Distinct().OrderBy(size => size).ToList();
            if (sizes.Count == 0)
                sizes.AddRange(DefaultSizes);

            var variantIds = new List<Guid>(sizes.Count);
            foreach (var size in sizes)
            {
                var existing = sneaker.Variants.FirstOrDefault(v => v.Size == size && v.Color == color);
                if (existing is not null)
                {
                    variantIds.Add(existing.Id);
                    continue;
                }

                var variant = new SneakerVariant
                {
                    SneakerId = sneaker.Id,
                    Sku = NextSku(sneaker.Slug, color, size, usedSkus),
                    Size = size,
                    Color = color
                };
                db.Variants.Add(variant);
                sneaker.Variants.Add(variant);
                created.Add(variant);
                variantIds.Add(variant.Id);
            }

            results.Add(new SneakerColorVariantsDto(sneaker.Id, variantIds));
        }

        if (created.Count == 0)
            return results;

        await db.SaveChangesAsync(ct);
        foreach (var sneakerId in created.Select(v => v.SneakerId).Distinct())
            await InvalidateAsync(sneakerId);

        foreach (var variant in created)
            await publisher.Publish(new SneakerVariantCreated(variant.Id, variant.SneakerId, variant.Sku), ct);

        return Created("/api/sneakers", results);
    }

    /// <summary>Add a size/color variant. Inventory is notified through an event and creates an empty stock record.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:guid}/variants")]
    public async Task<ActionResult<VariantDto>> AddVariant(Guid id, CreateVariantRequest request, CancellationToken ct)
    {
        if (!await db.Sneakers.AnyAsync(s => s.Id == id, ct)) return NotFound();

        var sku = request.Sku.Trim().ToUpperInvariant();
        var color = request.Color.Trim();

        if (await db.Variants.AnyAsync(v => v.Sku == sku, ct))
            throw new AppException(StatusCodes.Status409Conflict, "SKU already exists.");
        if (await db.Variants.AnyAsync(v => v.SneakerId == id && v.Size == request.Size && v.Color == color, ct))
            throw new AppException(StatusCodes.Status409Conflict, "This size and color already exist for the sneaker.");

        var variant = new SneakerVariant { SneakerId = id, Sku = sku, Size = request.Size, Color = color };
        db.Variants.Add(variant);
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id);

        await publisher.Publish(new SneakerVariantCreated(variant.Id, id, variant.Sku), ct);

        return Created($"/api/sneakers/{id}", new VariantDto(variant.Id, id, variant.Sku, variant.Size, variant.Color));
    }

    /// <summary>
    /// Runs after the database commit and ignores request cancellation, so a client disconnect cannot leave stale
    /// entries behind. The FusionCache backplane forwards the removal to every other Catalog instance.
    /// </summary>
    private async Task InvalidateAsync(Guid sneakerId)
    {
        await cache.RemoveAsync(CatalogCacheKeys.Sneaker(sneakerId));
        await cache.RemoveByTagAsync(CatalogCacheKeys.SneakerListTag);
    }

    private static readonly decimal[] DefaultSizes = [8m, 9m, 10m, 11m];

    private static string NextSku(string slug, string color, decimal size, HashSet<string> usedSkus)
    {
        var colorPart = Slug.From(color).ToUpperInvariant();
        if (colorPart.Length == 0)
            colorPart = "COLOR";

        var slugPart = string.IsNullOrEmpty(slug) ? "SKU" : slug.ToUpperInvariant();
        var sizePart = size.ToString("0.0", CultureInfo.InvariantCulture);
        var baseSku = $"{slugPart}-{colorPart}-{sizePart}";

        var sku = FitSku(baseSku, 64);
        var suffix = 2;
        while (!usedSkus.Add(sku))
        {
            var tail = $"-{suffix++}";
            sku = FitSku(baseSku, 64 - tail.Length) + tail;
        }

        return sku;
    }

    private static string FitSku(string value, int maxLength)
    {
        if (maxLength < 1)
            maxLength = 1;
        return (value.Length <= maxLength ? value : value[..maxLength]).Trim('-');
    }

    private static SneakerDetail ToDetail(Sneaker s) => new(
        s.Id, s.Name, s.Slug, s.Description,
        new BrandDto(s.Brand.Id, s.Brand.Name, s.Brand.Slug),
        s.Gender, s.Price, s.ImageUrl, s.IsActive, s.CreatedAt,
        s.Variants.OrderBy(v => v.Size).ThenBy(v => v.Color)
            .Select(v => new VariantDto(v.Id, v.SneakerId, v.Sku, v.Size, v.Color)).ToList());
}
