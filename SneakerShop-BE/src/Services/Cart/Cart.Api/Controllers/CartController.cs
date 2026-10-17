using Cart.Api.Dtos;
using Cart.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SneakerShop.BuildingBlocks;

namespace Cart.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController(ICartStore carts, ICatalogClient catalog, ICartCheckoutService checkout) : ControllerBase
{
    /// <summary>The signed-in customer's cart with current names and prices from Catalog.</summary>
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct) =>
        await BuildAsync(User.GetUserId(), ct);

    /// <summary>Add one or many variants to the same cart. Existing lines are incremented.</summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItems(AddCartItemsRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var additions = request.Items
            .GroupBy(item => item.VariantId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        if (additions.Any(line => line.Value > RedisCartStore.MaxQuantity))
            throw new AppException(StatusCodes.Status400BadRequest,
                $"You can add at most {RedisCartStore.MaxQuantity} pairs of the same variant.");

        var found = (await catalog.LookupAsync(additions.Keys.ToList(), ct))
            .Select(v => v.VariantId)
            .ToHashSet();
        var missing = additions.Keys.Where(id => !found.Contains(id)).ToList();
        if (missing.Count > 0)
            throw new AppException(StatusCodes.Status400BadRequest,
                $"Unknown or unavailable variant(s): {string.Join(", ", missing)}");

        var result = await carts.AddQuantitiesAsync(userId, additions);
        if (result == 0)
            throw new AppException(StatusCodes.Status409Conflict,
                $"A cart can hold at most {RedisCartStore.MaxLines} different items.");
        if (result < 0)
            throw new AppException(StatusCodes.Status400BadRequest,
                $"A cart line can hold at most {RedisCartStore.MaxQuantity} pairs.");

        return await BuildAsync(userId, ct);
    }

    /// <summary>Add a variant or change its quantity. A cart holds at most 50 different variants.</summary>
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

    /// <summary>
    /// Place an order from the current cart and start payment. Method <c>vnpay</c> returns a VNPay checkout URL.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType<CartCheckoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CartCheckoutResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<CartCheckoutResponse>> Checkout(
        CheckoutCartRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            return BadRequest(new ProblemDetails
            {
                Title = "A valid Idempotency-Key header is required.",
                Status = StatusCodes.Status400BadRequest
            });

        var result = await checkout.CheckoutAsync(User.GetUserId(), request, idempotencyKey.Trim(), ct);
        if (result.Payment is null)
            return AcceptedAtAction(nameof(Get), result);
        return Created($"/api/payments/{result.Payment.Id}", result);
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
