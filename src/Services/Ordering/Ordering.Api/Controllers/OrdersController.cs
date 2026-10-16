using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Domain;
using Ordering.Api.Dtos;
using Ordering.Api.Services;
using SneakerShop.BuildingBlocks;

namespace Ordering.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Place an order. It starts as Pending and becomes Confirmed or Rejected once stock is checked.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            return BadRequest(new ProblemDetails
            {
                Title = "A valid Idempotency-Key header is required.",
                Status = StatusCodes.Status400BadRequest
            });

        var order = await orders.CreateAsync(User.GetUserId(), request, idempotencyKey.Trim(), ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    /// <summary>The signed-in customer's orders, newest first.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<List<OrderDto>>> My(CancellationToken ct) =>
        await orders.ListMineAsync(User.GetUserId(), ct);

    /// <summary>Admin: all orders, optionally filtered by status.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await orders.ListAllAsync(status, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct) =>
        await orders.GetAsync(id, User.GetUserId(), User.IsAdmin(), ct);

    /// <summary>Cancel a Pending or Confirmed order. Reserved stock is released.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken ct) =>
        await orders.CancelAsync(id, User.GetUserId(), User.IsAdmin(), ct);

    /// <summary>Admin: mark a Shipped order as Delivered. Successful payment already moves the order to Shipped.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct) =>
        await orders.UpdateStatusAsync(id, request.Status, ct);
}
