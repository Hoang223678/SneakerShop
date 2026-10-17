using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using Ordering.Api.Dtos;
using Ordering.Api.Services;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Ordering.Api.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task Create_uses_catalog_prices_merges_duplicate_lines_and_publishes_reservation_event()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        await using var fixture = await TestFixture.CreateAsync(
            new VariantLookupItem(variantId, Guid.NewGuid(), "Runner", "RUN-9", 9m, "Black", 75m));
        var request = CreateRequest(
            new CreateOrderItem { VariantId = variantId, Quantity = 2 },
            new CreateOrderItem { VariantId = variantId, Quantity = 3 });

        var order = await fixture.Service.CreateAsync(userId, request, "idempotency-1", default);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(375m, order.TotalAmount);
        Assert.Single(order.Items);
        Assert.Equal(5, order.Items[0].Quantity);
        Assert.Equal(75m, order.Items[0].UnitPrice);
        Assert.InRange(order.PaymentExpiresAt, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(15));
        var persisted = await fixture.Db.Orders.Include(x => x.Items).SingleAsync();
        Assert.Equal(375m, persisted.TotalAmount);
        Assert.True(await fixture.Harness.Published.Any<OrderCreated>(x =>
            x.Context.Message.OrderId == order.Id &&
            x.Context.Message.Items.Count == 1 &&
            x.Context.Message.Items[0].Quantity == 5));
    }

    [Fact]
    public async Task Create_is_idempotent_for_same_customer_and_key()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        await using var fixture = await TestFixture.CreateAsync(
            new VariantLookupItem(variantId, Guid.NewGuid(), "Runner", "RUN-9", 9m, "Black", 75m));
        var request = CreateRequest(new CreateOrderItem { VariantId = variantId, Quantity = 1 });

        var first = await fixture.Service.CreateAsync(userId, request, "stable-key", default);
        var retry = await fixture.Service.CreateAsync(userId, request, "stable-key", default);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, await fixture.Db.Orders.CountAsync());
        Assert.Equal(1, await fixture.Harness.Published.SelectAsync<OrderCreated>().Count());
    }

    [Fact]
    public async Task Create_rejects_merged_quantity_above_limit_before_catalog_lookup()
    {
        var variantId = Guid.NewGuid();
        await using var fixture = await TestFixture.CreateAsync();
        var request = CreateRequest(
            new CreateOrderItem { VariantId = variantId, Quantity = 600_000 },
            new CreateOrderItem { VariantId = variantId, Quantity = 500_001 });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(Guid.NewGuid(), request, "idempotency-1", default));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal(0, fixture.Catalog.LookupCallCount);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateFromCart_places_available_cart_lines()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        await using var fixture = await TestFixture.CreateAsync(
            new VariantLookupItem(variantId, Guid.NewGuid(), "Runner", "RUN-9", 9m, "Black", 80m));
        fixture.Cart.Items = [new CartSnapshotItem(variantId, 2, true)];

        var order = await fixture.Service.CreateFromCartAsync(userId, new CreateOrderFromCartRequest
        {
            ShippingName = "Test Customer",
            ShippingPhone = "0900000000",
            ShippingAddress = "Test address"
        }, "cart-key", default);

        Assert.Equal(160m, order.TotalAmount);
        Assert.Equal(2, order.Items[0].Quantity);
    }

    [Fact]
    public async Task CreateFromCart_rejects_empty_cart()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateFromCartAsync(Guid.NewGuid(), new CreateOrderFromCartRequest
            {
                ShippingName = "Test Customer",
                ShippingPhone = "0900000000",
                ShippingAddress = "Test address"
            }, "cart-key", default));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal(0, fixture.Catalog.LookupCallCount);
    }

    [Fact]
    public async Task Create_rejects_unavailable_variant()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var request = CreateRequest(new CreateOrderItem { VariantId = Guid.NewGuid(), Quantity = 1 });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(Guid.NewGuid(), request, "idempotency-1", default));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_sold_out_flash_sale_before_catalog_lookup()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.FlashSales.SoldOut = true;
        var request = CreateRequest(new CreateOrderItem { VariantId = Guid.NewGuid(), Quantity = 1 });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(Guid.NewGuid(), request, "idempotency-1", default));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal(0, fixture.Catalog.LookupCallCount);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task Create_returns_flash_sale_pairs_when_order_is_not_placed()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var request = CreateRequest(new CreateOrderItem { VariantId = Guid.NewGuid(), Quantity = 2 });

        await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(Guid.NewGuid(), request, "idempotency-1", default));

        Assert.Equal(1, fixture.FlashSales.ReleaseCount);
    }

    [Fact]
    public async Task Create_keeps_flash_sale_pairs_for_placed_order_and_does_not_retake_them_on_retry()
    {
        var variantId = Guid.NewGuid();
        await using var fixture = await TestFixture.CreateAsync(
            new VariantLookupItem(variantId, Guid.NewGuid(), "Runner", "RUN-9", 9m, "Black", 75m));
        var userId = Guid.NewGuid();
        var request = CreateRequest(new CreateOrderItem { VariantId = variantId, Quantity = 1 });

        await fixture.Service.CreateAsync(userId, request, "stable-key", default);
        await fixture.Service.CreateAsync(userId, request, "stable-key", default);

        Assert.Equal(1, fixture.FlashSales.AdmitCount);
        Assert.Equal(0, fixture.FlashSales.ReleaseCount);
    }

    [Fact]
    public async Task Get_hides_another_customers_order()
    {
        var order = SeedOrder(Guid.NewGuid());
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.Orders.Add(order);
        await fixture.Db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetAsync(order.Id, Guid.NewGuid(), isAdmin: false, default));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task List_all_clamps_paging_and_applies_status_filter()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.Orders.AddRange(
            SeedOrder(Guid.NewGuid(), OrderStatus.Confirmed),
            SeedOrder(Guid.NewGuid(), OrderStatus.Cancelled));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.ListAllAsync(OrderStatus.Confirmed, page: 0, pageSize: 1000, default);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(OrderStatus.Confirmed, result.Items[0].Status);
    }

    [Fact]
    public async Task Cancel_publishes_inventory_compensation()
    {
        var userId = Guid.NewGuid();
        var order = SeedOrder(userId, OrderStatus.Confirmed);
        order.IsPaid = true;
        order.PaymentId = Guid.NewGuid();
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.Orders.Add(order);
        await fixture.Db.SaveChangesAsync();

        var cancelled = await fixture.Service.CancelAsync(order.Id, userId, isAdmin: false, default);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.True(await fixture.Harness.Published.Any<OrderCancelled>(x =>
            x.Context.Message.OrderId == order.Id));
        Assert.True(await fixture.Harness.Published.Any<RefundRequested>(x =>
            x.Context.Message.OrderId == order.Id &&
            x.Context.Message.PaymentId == order.PaymentId &&
            x.Context.Message.RefundRemaining));
    }

    [Fact]
    public async Task Shipping_requires_payment_and_publishes_inventory_event_when_paid()
    {
        var order = SeedOrder(Guid.NewGuid(), OrderStatus.Confirmed);
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.Orders.Add(order);
        await fixture.Db.SaveChangesAsync();

        var unpaidException = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.UpdateStatusAsync(order.Id, OrderStatus.Shipped, default));
        Assert.Equal(StatusCodes.Status409Conflict, unpaidException.StatusCode);

        order.IsPaid = true;
        await fixture.Db.SaveChangesAsync();
        var shipped = await fixture.Service.UpdateStatusAsync(order.Id, OrderStatus.Shipped, default);

        Assert.Equal(OrderStatus.Shipped, shipped.Status);
        Assert.True(await fixture.Harness.Published.Any<OrderShipped>(x =>
            x.Context.Message.OrderId == order.Id));
    }

    private static CreateOrderRequest CreateRequest(params CreateOrderItem[] items) => new()
    {
        ShippingName = "Test Customer",
        ShippingPhone = "0900000000",
        ShippingAddress = "Test address",
        Items = items.ToList()
    };

    private static Order SeedOrder(Guid userId, OrderStatus status = OrderStatus.Pending) => new()
    {
        UserId = userId,
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        Status = status,
        TotalAmount = 100m,
        PaymentExpiresAt = DateTime.UtcNow.AddMinutes(15),
        ShippingName = "Test",
        ShippingPhone = "0900000000",
        ShippingAddress = "Test address"
    };

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;

        private TestFixture(ServiceProvider provider, ITestHarness harness, FakeCatalogClient catalog, FakeCartClient cart)
        {
            this.provider = provider;
            Harness = harness;
            Catalog = catalog;
            Cart = cart;
            Db = provider.GetRequiredService<OrderDbContext>();
            Service = new OrderService(Db, catalog, cart, provider.GetRequiredService<IPublishEndpoint>(), FlashSales);
        }

        public OrderDbContext Db { get; }
        public IOrderService Service { get; }
        public ITestHarness Harness { get; }
        public FakeCatalogClient Catalog { get; }
        public FakeCartClient Cart { get; }
        public FakeFlashSaleGate FlashSales { get; } = new();

        public static async Task<TestFixture> CreateAsync(params VariantLookupItem[] variants)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<OrderDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            var catalog = new FakeCatalogClient(variants);
            var cart = new FakeCartClient();
            services.AddSingleton<ICatalogClient>(catalog);
            services.AddSingleton<ICartClient>(cart);
            services.AddMassTransitTestHarness();
            var provider = services.BuildServiceProvider();
            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();
            return new TestFixture(provider, harness, catalog, cart);
        }

        public async ValueTask DisposeAsync() => await provider.DisposeAsync();
    }

    /// <summary>Behaves as if every variant in the order is in a running flash sale.</summary>
    private sealed class FakeFlashSaleGate : IFlashSaleGate
    {
        public bool SoldOut { get; set; }
        public int AdmitCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public Task<FlashSaleTicket?> TryAdmitAsync(IReadOnlyList<OrderLine> lines)
        {
            if (SoldOut) return Task.FromResult<FlashSaleTicket?>(null);
            AdmitCount++;
            return Task.FromResult<FlashSaleTicket?>(new FlashSaleTicket(lines));
        }

        public Task ReleaseAsync(FlashSaleTicket ticket)
        {
            if (ticket.Taken.Count > 0) ReleaseCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCartClient : ICartClient
    {
        public IReadOnlyList<CartSnapshotItem> Items { get; set; } = [];

        public Task<CartSnapshot> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new CartSnapshot(Items));
    }

    private sealed class FakeCatalogClient(params VariantLookupItem[] variants) : ICatalogClient
    {
        public int LookupCallCount { get; private set; }

        public Task<IReadOnlyList<VariantLookupItem>> LookupAsync(
            IReadOnlyList<Guid> variantIds, CancellationToken ct)
        {
            LookupCallCount++;
            IReadOnlyList<VariantLookupItem> results = variants
                .Where(x => variantIds.Contains(x.VariantId))
                .ToList();
            return Task.FromResult(results);
        }
    }
}
