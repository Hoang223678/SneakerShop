using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using Ordering.Api.Sagas;
using SneakerShop.Contracts;

namespace Ordering.Api.Tests;

public class OrderStateMachineTests
{
    [Fact]
    public async Task Reservation_failure_rejects_the_order()
    {
        var orderId = Guid.NewGuid();
        await using var provider = CreateProvider();
        await SeedOrderAsync(provider, orderId);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await harness.Bus.Publish(new OrderCreated(
            orderId, Guid.NewGuid(), Array.Empty<OrderLine>(), DateTime.UtcNow.AddMinutes(15)));
        Assert.True(await harness.Consumed.Any<OrderCreated>());

        await harness.Bus.Publish(new StockReservationFailed(orderId, "No stock."));
        Assert.True(await harness.Consumed.Any<StockReservationFailed>());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == orderId);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("No stock.", order.FailureReason);
    }

    [Fact]
    public async Task Reservation_timeout_cancels_the_order_and_publishes_compensation()
    {
        var orderId = Guid.NewGuid();
        await using var provider = CreateProvider();
        await SeedOrderAsync(provider, orderId);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await harness.Bus.Publish(new OrderCreated(
            orderId, Guid.NewGuid(), Array.Empty<OrderLine>(), DateTime.UtcNow.AddMinutes(15)));
        Assert.True(await harness.Consumed.Any<OrderCreated>());

        await harness.Bus.Publish(new OrderReservationTimedOut(orderId));
        Assert.True(await harness.Consumed.Any<OrderReservationTimedOut>());
        Assert.True(await harness.Published.Any<OrderCancelled>());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task Stock_reserved_arriving_before_order_created_still_confirms()
    {
        var orderId = Guid.NewGuid();
        await using var provider = CreateProvider();
        await SeedOrderAsync(provider, orderId);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new StockReserved(orderId));
        await harness.Bus.Publish(new OrderCreated(
            orderId, Guid.NewGuid(), Array.Empty<OrderLine>(), DateTime.UtcNow.AddMinutes(15)));

        await WaitForAsync(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            return await db.Orders.AnyAsync(x => x.Id == orderId && x.Status == OrderStatus.Confirmed);
        });
    }

    [Fact]
    public async Task Payment_failure_keeps_confirmed_order_available_for_retry()
    {
        var orderId = Guid.NewGuid();
        await using var provider = CreateProvider();
        await SeedOrderAsync(provider, orderId);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await StartConfirmedOrderAsync(harness, orderId);

        await harness.Bus.Publish(new PaymentFailed(orderId, Guid.NewGuid(), 100m, "Declined."));
        Assert.True(await harness.Consumed.Any<PaymentFailed>());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == orderId);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.False(order.IsPaid);
    }

    [Fact]
    public async Task Payment_success_marks_order_paid_and_ships_it()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        await using var provider = CreateProvider();
        await SeedOrderAsync(provider, orderId);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await StartConfirmedOrderAsync(harness, orderId);

        await harness.Bus.Publish(new PaymentSucceeded(orderId, paymentId, 100m));
        Assert.True(await harness.Consumed.Any<PaymentSucceeded>());
        Assert.True(await harness.Published.Any<OrderPaid>());
        Assert.True(await harness.Published.Any<OrderShipped>(x => x.Context.Message.OrderId == orderId));

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == orderId);
        Assert.True(order.IsPaid);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal(paymentId, order.PaymentId);
    }

    [Fact]
    public async Task Successful_payment_after_expiry_publishes_refund_instead_of_marking_order_paid()
    {
        var orderId = Guid.NewGuid();
        await using var provider = CreateProvider();
        await SeedOrderAsync(provider, orderId, DateTime.UtcNow.AddMinutes(-1));
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await StartConfirmedOrderAsync(harness, orderId);

        await harness.Bus.Publish(new PaymentSucceeded(orderId, Guid.NewGuid(), 100m));

        Assert.True(await harness.Published.Any<RefundRequested>(x =>
            x.Context.Message.OrderId == orderId && x.Context.Message.RefundRemaining));
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.False((await db.Orders.SingleAsync(x => x.Id == orderId)).IsPaid);
    }

    private static async Task StartConfirmedOrderAsync(ITestHarness harness, Guid orderId)
    {
        await harness.Bus.Publish(new OrderCreated(
            orderId, Guid.NewGuid(), Array.Empty<OrderLine>(), DateTime.UtcNow.AddMinutes(15)));
        Assert.True(await harness.Consumed.Any<OrderCreated>());
        await harness.Bus.Publish(new StockReserved(orderId));
        Assert.True(await harness.Consumed.Any<StockReserved>());
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<OrderDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<OrderStateMachine, OrderState>().InMemoryRepository();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });
        return services.BuildServiceProvider();
    }

    private static async Task SeedOrderAsync(
        IServiceProvider provider, Guid orderId, DateTime? paymentExpiresAt = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Orders.Add(new Order
        {
            Id = orderId,
            UserId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            TotalAmount = 100m,
            PaymentExpiresAt = paymentExpiresAt ?? DateTime.UtcNow.AddMinutes(15),
            ShippingName = "Test",
            ShippingPhone = "0900000000",
            ShippingAddress = "Test address"
        });
        await db.SaveChangesAsync();
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }

        throw new TimeoutException("The order did not reach the expected state.");
    }
}
