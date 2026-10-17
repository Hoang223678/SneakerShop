using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Dtos;
using Ordering.Api.Services;
using SneakerShop.BuildingBlocks;

namespace Ordering.Api.Controllers;

[ApiController]
[Route("api/flash-sales")]
public class FlashSalesController(IFlashSaleAdmin flashSales) : ControllerBase
{
    /// <summary>Public: pairs left in a running flash sale and when it ends.</summary>
    [HttpGet("{variantId:guid}")]
    public async Task<ActionResult<FlashSaleStatus>> Get(Guid variantId) =>
        await flashSales.GetAsync(variantId) is { } status ? status : NotFound();

    /// <summary>Admin: start or reset a flash sale for one variant. The counter disappears when the sale ends.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{variantId:guid}")]
    public async Task<ActionResult<FlashSaleStatus>> Start(Guid variantId, StartFlashSaleRequest request)
    {
        var endsAt = request.EndsAt!.Value;
        var now = DateTimeOffset.UtcNow;
        if (endsAt <= now || endsAt > now.AddDays(30))
            throw new AppException(StatusCodes.Status400BadRequest, "EndsAt must be in the future and at most 30 days away.");

        return await flashSales.StartAsync(variantId, request.Quantity, endsAt);
    }

    /// <summary>Admin: end a flash sale early.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{variantId:guid}")]
    public async Task<IActionResult> End(Guid variantId) =>
        await flashSales.EndAsync(variantId) ? NoContent() : NotFound();
}
