using Catalog.Api.Caching;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.Dtos;
using Catalog.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using ZiggyCreatures.Caching.Fusion;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/brands")]
public class BrandsController(CatalogDbContext db, IFusionCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> List(CancellationToken ct) =>
        await cache.GetOrSetAsync<List<BrandDto>>(
            CatalogCacheKeys.Brands,
            token => db.Brands.AsNoTracking().OrderBy(b => b.Name)
                .Select(b => new BrandDto(b.Id, b.Name, b.Slug)).ToListAsync(token),
            CatalogCacheKeys.BrandsDuration,
            ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<BrandDto>> Create(CreateBrandRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var slug = Slug.From(name);
        if (slug.Length == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "Brand name must contain letters or digits.");
        if (await db.Brands.AnyAsync(b => b.Name == name, ct))
            throw new AppException(StatusCodes.Status409Conflict, "Brand already exists.");

        var brand = new Brand { Name = name, Slug = slug };
        db.Brands.Add(brand);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CatalogCacheKeys.Brands);

        return CreatedAtAction(nameof(List), new BrandDto(brand.Id, brand.Name, brand.Slug));
    }
}
