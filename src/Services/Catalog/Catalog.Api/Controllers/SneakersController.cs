using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.Dtos;
using Catalog.Api.Helpers;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/sneakers")]
public class SneakersController(CatalogDbContext db, IPublishEndpoint publisher) : ControllerBase
{
    /// <summary>Search and filter the catalog (active products only).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SneakerListItem>>> List([FromQuery] SneakerQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

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
        var sneaker = await db.Sneakers.AsNoTracking()
            .Include(s => s.Brand)
            .Include(s => s.Variants)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        // Inactive (soft-deleted) products are only visible to admins.
        if (sneaker is null || (!sneaker.IsActive && !User.IsAdmin()))
            return NotFound();

        return ToDetail(sneaker);
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
        return NoContent();
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

        await publisher.Publish(new SneakerVariantCreated(variant.Id, id, variant.Sku), ct);

        return Created($"/api/sneakers/{id}", new VariantDto(variant.Id, variant.Sku, variant.Size, variant.Color));
    }

    private static SneakerDetail ToDetail(Sneaker s) => new(
        s.Id, s.Name, s.Slug, s.Description,
        new BrandDto(s.Brand.Id, s.Brand.Name, s.Brand.Slug),
        s.Gender, s.Price, s.ImageUrl, s.IsActive, s.CreatedAt,
        s.Variants.OrderBy(v => v.Size).ThenBy(v => v.Color)
            .Select(v => new VariantDto(v.Id, v.Sku, v.Size, v.Color)).ToList());
}
