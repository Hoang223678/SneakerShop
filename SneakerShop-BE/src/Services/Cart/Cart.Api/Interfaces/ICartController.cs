using Cart.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Cart.Api.Interfaces;

public interface ICartController
{
    Task<ActionResult<CartDto>> Get(CancellationToken ct);
    Task<ActionResult<CartDto>> AddItems(AddCartItemsRequest request, CancellationToken ct);
    Task<ActionResult<CartDto>> SetItem(Guid variantId, SetCartItemRequest request, CancellationToken ct);
    Task<ActionResult<CartDto>> RemoveItem(Guid variantId, CancellationToken ct);
    Task<IActionResult> Clear();
    Task<ActionResult<CartCheckoutResponse>> Checkout(CheckoutCartRequest request, string? idempotencyKey, CancellationToken ct);
}
