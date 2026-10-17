using Cart.Api.Dtos;
using Cart.Api.Services;
using Microsoft.AspNetCore.Mvc;
using SneakerShop.BuildingBlocks;

namespace Cart.Api.Controllers;

[ApiController]
[Route("internal/cart")]
[InternalApiKey]
public sealed class InternalCartController(ICartStore carts, ICatalogClient catalog) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<InternalCartDto>> Get(Guid userId, CancellationToken ct)
    {
        var lines = await carts.GetAsync(userId);
        if (lines.Count == 0)
            return Ok(new InternalCartDto([]));

        var available = (await catalog.LookupAsync(lines.Keys.ToList(), ct))
            .Select(v => v.VariantId)
            .ToHashSet();
        var items = lines
            .Select(line => new InternalCartLineDto(line.Key, line.Value, available.Contains(line.Key)))
            .ToList();
        return Ok(new InternalCartDto(items));
    }
}
