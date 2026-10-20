using Catalog.Api.Data;
using Catalog.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Catalog.Api.Controllers;

/// <summary>
/// Service-to-service endpoint used by Ordering to get authoritative names and prices.
/// Protected by a shared key and NOT exposed through the API gateway.
/// </summary>
[ApiController]
[Route("internal/variants")]
[InternalApiKey]
public class InternalVariantsController(CatalogDbContext db) : ControllerBase, IInternalVariantsController
{
    [HttpPost("lookup")]
    public async Task<ActionResult<List<VariantLookupItem>>> Lookup(VariantLookupRequest request, CancellationToken ct)
    {
        var ids = request.VariantIds.Distinct().ToList();

        return await db.Variants.AsNoTracking()
            .Where(v => ids.Contains(v.Id) && v.Sneaker.IsActive)
            .Select(v => new VariantLookupItem(
                v.Id, v.SneakerId, v.Sneaker.Name, v.Sku, v.Size, v.Color, v.Sneaker.Price))
            .ToListAsync(ct);
    }
}
