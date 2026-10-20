using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Domain;
using Ordering.Api.Dtos;
using SneakerShop.BuildingBlocks;

namespace Ordering.Api.Interfaces;

public interface IOrdersController
{
    Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, string? idempotencyKey, CancellationToken ct);
    Task<ActionResult<OrderDto>> CreateFromCart(CreateOrderFromCartRequest request, string? idempotencyKey, CancellationToken ct);
    Task<ActionResult<List<OrderDto>>> My(CancellationToken ct);
    Task<ActionResult<DashboardDto>> Dashboard(int? year, CancellationToken ct);
    Task<ActionResult<PagedResult<OrderDto>>> List(OrderStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct);
    Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken ct);
    Task<ActionResult<OrderDto>> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct);
}
