using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Dtos;

namespace Ordering.Api.Interfaces;

public interface IFlashSalesController
{
    Task<ActionResult<FlashSaleStatus>> Get(Guid variantId);
    Task<ActionResult<FlashSaleStatus>> Start(Guid variantId, StartFlashSaleRequest request);
    Task<IActionResult> End(Guid variantId);
}
