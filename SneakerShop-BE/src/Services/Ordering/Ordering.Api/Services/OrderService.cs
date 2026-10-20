using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using Ordering.Api.Dtos;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Ordering.Api.Services;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, string idempotencyKey, CancellationToken ct);
    Task<OrderDto> CreateFromCartAsync(Guid userId, CreateOrderFromCartRequest request, string idempotencyKey, CancellationToken ct);
    Task<OrderDto> GetAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct);
    Task<List<OrderDto>> ListMineAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<OrderDto>> ListAllAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct);
    Task<DashboardDto> GetDashboardAsync(int? year, CancellationToken ct);
    Task<OrderDto> CancelAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct);
    Task<OrderDto> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct);
}

public class OrderService(
    OrderDbContext db, ICatalogClient catalog, ICartClient carts, IPublishEndpoint publisher, IFlashSaleGate flashSales) : IOrderService
{
    public async Task<OrderDto> CreateFromCartAsync(
        Guid userId, CreateOrderFromCartRequest request, string idempotencyKey, CancellationToken ct)
    {
        var cart = await carts.GetAsync(userId, ct);
        if (cart.Items.Count == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "The cart is empty.");

        var unavailable = cart.Items.Where(item => !item.Available).Select(item => item.VariantId).ToList();
        if (unavailable.Count > 0)
            throw new AppException(StatusCodes.Status400BadRequest,
                $"Unknown or unavailable variant(s): {string.Join(", ", unavailable)}");

        return await CreateAsync(userId, new CreateOrderRequest
        {
            ShippingName = request.ShippingName,
            ShippingPhone = request.ShippingPhone,
            ShippingAddress = request.ShippingAddress,
            Items = cart.Items.Select(item => new CreateOrderItem
            {
                VariantId = item.VariantId,
                Quantity = item.Quantity
            }).ToList()
        }, idempotencyKey, ct);
    }

    public async Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, string idempotencyKey, CancellationToken ct)
    {
        var existingOrder = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == idempotencyKey, ct);
        if (existingOrder is not null) return existingOrder.ToDto();

        // Merge duplicate lines of the same variant.
        var lines = request.Items
            .GroupBy(i => i.VariantId)
            .Select(g => new OrderLine(g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        if (lines.Any(l => l.Quantity > 1_000_000))
            throw new AppException(StatusCodes.Status400BadRequest, "You can order at most 1000000 pairs of the same variant.");

        var ticket = await flashSales.TryAdmitAsync(lines)
            ?? throw new AppException(StatusCodes.Status409Conflict, "This flash sale is sold out.");
        var created = false;
        try
        {
            (var order, created) = await PlaceAsync(userId, request, idempotencyKey, lines, ct);
            return order.ToDto();
        }
        finally
        {
            if (!created) await flashSales.ReleaseAsync(ticket);
        }
    }

    /// <summary>Returns the order and whether this call created it (false when a concurrent retry won the race).</summary>
    private async Task<(Order Order, bool Created)> PlaceAsync(
        Guid userId, CreateOrderRequest request, string idempotencyKey, List<OrderLine> lines, CancellationToken ct)
    {
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
            IdempotencyKey = idempotencyKey,
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(15),
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
        await publisher.Publish(new OrderCreated(order.Id, userId, lines, order.PaymentExpiresAt), ct);
        try
        {
            // The EF bus outbox persists the order and message in one database transaction.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            db.ChangeTracker.Clear();
            var concurrentOrder = await db.Orders.AsNoTracking().Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == idempotencyKey, ct);
            if (concurrentOrder is null) throw;
            return (concurrentOrder, false);
        }

        return (order, true);
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

    public async Task<DashboardDto> GetDashboardAsync(int? year, CancellationToken ct)
    {
        var zone = ShopClock.Zone;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var selectedYear = year is >= 2000 and <= 2100 ? year.Value : nowLocal.Year;
        var month = selectedYear == nowLocal.Year ? nowLocal.Month : 12;

        var fromLocal = new DateTime(selectedYear - 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var toLocal = new DateTime(selectedYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, zone);

        var paid = await db.Orders.AsNoTracking()
            .Where(o => o.IsPaid && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new PaidLine(o.CreatedAt, o.TotalAmount, o.Items.Sum(i => i.Quantity)))
            .ToListAsync(ct);

        var rows = paid.Select(o =>
        {
            var local = ShopClock.ToLocal(o.CreatedAt, zone);
            return new { local.Year, local.Month, o.TotalAmount, o.Pairs };
        }).ToList();

        var prevMonth = month == 1 ? 12 : month - 1;
        var prevMonthYear = month == 1 ? selectedYear - 1 : selectedYear;

        decimal SumRevenue(int y, int? m = null) =>
            rows.Where(r => r.Year == y && (m is null || r.Month == m)).Sum(r => r.TotalAmount);
        int SumPairs(int y, int? m = null) =>
            rows.Where(r => r.Year == y && (m is null || r.Month == m)).Sum(r => r.Pairs);

        var monthly = Enumerable.Range(1, 12)
            .Select(m => new DashboardMonthPoint(m, SumRevenue(selectedYear, m), SumPairs(selectedYear, m)))
            .ToList();

        return new DashboardDto(
            selectedYear,
            month,
            SumRevenue(selectedYear, month),
            SumRevenue(prevMonthYear, prevMonth),
            SumRevenue(selectedYear),
            SumRevenue(selectedYear - 1),
            SumPairs(selectedYear, month),
            SumPairs(prevMonthYear, prevMonth),
            SumPairs(selectedYear),
            SumPairs(selectedYear - 1),
            rows.Count(r => r.Year == selectedYear && r.Month == month),
            monthly);
    }

    public async Task<OrderDto> CancelAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var order = await FindTrackedAsync(orderId, ct);
        if (!isAdmin && order.UserId != userId)
            throw new AppException(StatusCodes.Status404NotFound, "Order not found.");

        if (!order.TryCancel())
            throw new AppException(StatusCodes.Status409Conflict, $"An order that is {order.Status} cannot be cancelled.");

        // Compensation: Inventory releases any stock it reserved for this order.
        await publisher.Publish(new OrderCancelled(order.Id), ct);
        if (order.IsPaid && order.PaymentId is Guid paymentId)
            await publisher.Publish(new RefundRequested(order.Id, order.Id, paymentId, 0, RefundRemaining: true), ct);
        await SaveAsync(ct);
        return order.ToDto();
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
    {
        if (newStatus is not (OrderStatus.Shipped or OrderStatus.Delivered))
            throw new AppException(StatusCodes.Status400BadRequest, "Only Shipped and Delivered can be set manually.");

        var order = await FindTrackedAsync(orderId, ct);

        if (newStatus == OrderStatus.Shipped && !order.IsPaid)
            throw new AppException(StatusCodes.Status409Conflict, "An order must be paid before it can be shipped.");

        var ok = newStatus == OrderStatus.Shipped ? order.TryShip() : order.TryDeliver();
        if (!ok)
            throw new AppException(StatusCodes.Status409Conflict, $"Cannot move an order from {order.Status} to {newStatus}.");

        if (newStatus == OrderStatus.Shipped)
        {
            var lines = order.Items.Select(i => new OrderLine(i.VariantId, i.Quantity)).ToList();
            await publisher.Publish(new OrderShipped(order.Id, lines), ct);
        }

        await SaveAsync(ct);
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

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private readonly record struct PaidLine(DateTime CreatedAt, decimal TotalAmount, int Pairs);

    private static class ShopClock
    {
        public static TimeZoneInfo Zone
        {
            get
            {
                foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
                {
                    try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                    catch (TimeZoneNotFoundException) { }
                    catch (InvalidTimeZoneException) { }
                }
                return TimeZoneInfo.Utc;
            }
        }

        public static DateTime ToLocal(DateTime value, TimeZoneInfo zone)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        }
    }
}
