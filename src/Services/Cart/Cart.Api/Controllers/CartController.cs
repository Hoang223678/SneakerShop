using Cart.Api.Dtos;
using Cart.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SneakerShop.BuildingBlocks;

namespace Cart.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController(ICartStore carts, ICatalogClient catalog) : ControllerBase
{
    /// <summary>The signed-in customer's cart with current names and prices from Catalog.</summary>
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct) =>
        await BuildAsync(User.GetUserId(), ct);

    /// <summary>Add a variant or change its quantity (1-10 pairs). A cart holds at most 50 different variants.</summary>
    [HttpPut("items/{variantId:guid}")]
    public async Task<ActionResult<CartDto>> SetItem(Guid variantId, SetCartItemRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if ((await catalog.LookupAsync([variantId], ct)).Count == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "Unknown or unavailable variant.");

        if (!await carts.SetQuantityAsync(userId, variantId, request.Quantity))
            throw new AppException(StatusCodes.Status409Conflict,
                $"A cart can hold at most {RedisCartStore.MaxLines} different items.");

        return await BuildAsync(userId, ct);
    }

    [HttpDelete("items/{variantId:guid}")]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid variantId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await carts.RemoveAsync(userId, [variantId]);
        return await BuildAsync(userId, ct);
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await carts.ClearAsync(User.GetUserId());
        return NoContent();
    }

    private async Task<CartDto> BuildAsync(Guid userId, CancellationToken ct)
    {
        var lines = await carts.GetAsync(userId);
        if (lines.Count == 0) return new CartDto([], 0m);

        var variants = (await catalog.LookupAsync(lines.Keys.ToList(), ct)).ToDictionary(v => v.VariantId);
        var items = lines
            .Select(line => variants.TryGetValue(line.Key, out var v)
                ? new CartItemDto(line.Key, line.Value, true, v.SneakerId, v.SneakerName, v.Sku, v.Size, v.Color, v.UnitPrice)
                : new CartItemDto(line.Key, line.Value, false, null, null, null, null, null, null))
            .OrderBy(i => i.SneakerName).ThenBy(i => i.Size)
            .ToList();

        return new CartDto(items, items.Where(i => i.Available).Sum(i => i.UnitPrice!.Value * i.Quantity));
    }
}
