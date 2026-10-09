using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using Ordering.Api.Dtos;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Ordering.Api.Services;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct);
    Task<OrderDto> GetAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct);
    Task<List<OrderDto>> ListMineAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<OrderDto>> ListAllAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct);
    Task<OrderDto> CancelAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct);
    Task<OrderDto> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct);
}

public class OrderService(OrderDbContext db, ICatalogClient catalog, IPublishEndpoint publisher) : IOrderService
{
    public async Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct)
    {
        // Merge duplicate lines of the same variant.
        var lines = request.Items
            .GroupBy(i => i.VariantId)
            .Select(g => new OrderLine(g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        if (lines.Any(l => l.Quantity > 10))
            throw new AppException(StatusCodes.Status400BadRequest, "You can order at most 10 pairs of the same variant.");

        // Never trust prices from the client: ask Catalog for the real name/price of every variant.
        var variants = (await catalog.LookupAsync(lines.Select(l => l.VariantId).ToList(), ct))
            .ToDictionary(v => v.VariantId);

        var missing = lines.Where(l => !variants.ContainsKey(l.VariantId)).Select(l => l.VariantId).ToList();
        if (missing.Count > 0)
            throw new AppException(StatusCodes.Status400BadRequest,
                $"Unknown or unavailable variant(s): {string.Join(", ", missing)}");

        var order = new Order
        {
            UserId = userId,
            ShippingName = request.ShippingName.Trim(),
            ShippingPhone = request.ShippingPhone.Trim(),
            ShippingAddress = request.ShippingAddress.Trim()
        };

        foreach (var line in lines)
        {
            var v = variants[line.VariantId];
            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                VariantId = v.VariantId,
                SneakerId = v.SneakerId,
                SneakerName = v.SneakerName,
                Sku = v.Sku,
                Size = v.Size,
                Color = v.Color,
                UnitPrice = v.UnitPrice,
                Quantity = line.Quantity
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Start the saga: Inventory will answer with StockReserved or StockReservationFailed.
        await publisher.Publish(new OrderCreated(order.Id, userId, lines), ct);

        return order.ToDto();
    }

    public async Task<OrderDto> GetAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || (!isAdmin && order.UserId != userId))
            throw new AppException(StatusCodes.Status404NotFound, "Order not found.");
        return order.ToDto();
    }

    public async Task<List<OrderDto>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return orders.Select(o => o.ToDto()).ToList();
    }

    public async Task<PagedResult<OrderDto>> ListAllAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Orders.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);

        var total = await query.CountAsync(ct);
        var orders = await query.Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(orders.Select(o => o.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<OrderDto> CancelAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var order = await FindTrackedAsync(orderId, ct);
        if (!isAdmin && order.UserId != userId)
            throw new AppException(StatusCodes.Status404NotFound, "Order not found.");

        if (!order.TryCancel())
            throw new AppException(StatusCodes.Status409Conflict, $"An order that is {order.Status} cannot be cancelled.");

        await SaveAsync(ct);

        // Compensation: Inventory releases any stock it reserved for this order.
        await publisher.Publish(new OrderCancelled(order.Id), ct);
        return order.ToDto();
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
    {
        if (newStatus is not (OrderStatus.Shipped or OrderStatus.Delivered))
            throw new AppException(StatusCodes.Status400BadRequest, "Only Shipped and Delivered can be set manually.");

        var order = await FindTrackedAsync(orderId, ct);

        var ok = newStatus == OrderStatus.Shipped ? order.TryShip() : order.TryDeliver();
        if (!ok)
            throw new AppException(StatusCodes.Status409Conflict, $"Cannot move an order from {order.Status} to {newStatus}.");

        await SaveAsync(ct);

        if (newStatus == OrderStatus.Shipped)
        {
            var lines = order.Items.Select(i => new OrderLine(i.VariantId, i.Quantity)).ToList();
            await publisher.Publish(new OrderShipped(order.Id, lines), ct);
        }

        return order.ToDto();
    }

    private async Task<Order> FindTrackedAsync(Guid orderId, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new AppException(StatusCodes.Status404NotFound, "Order not found.");

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(StatusCodes.Status409Conflict, "The order was changed by another request. Please retry.");
        }
    }
}
