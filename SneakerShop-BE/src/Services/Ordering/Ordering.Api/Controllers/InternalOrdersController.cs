using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Interfaces;
using Ordering.Api.Domain;
using SneakerShop.BuildingBlocks;

namespace Ordering.Api.Controllers;

[ApiController]
[Route("internal/orders")]
[InternalApiKey]
public sealed class InternalOrdersController(OrderDbContext db) : ControllerBase, IInternalOrdersController
{
    [HttpGet("{id:guid}/payment-details")]
    public async Task<ActionResult<OrderPaymentDetails>> GetPaymentDetails(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return order is null
            ? NotFound()
            : Ok(new OrderPaymentDetails(order.Id, order.UserId, order.Status, order.TotalAmount, order.PaymentExpiresAt));
    }
}

public sealed record OrderPaymentDetails(
    Guid OrderId,
    Guid UserId,
    OrderStatus Status,
    decimal Amount,
    DateTime PaymentExpiresAt);
