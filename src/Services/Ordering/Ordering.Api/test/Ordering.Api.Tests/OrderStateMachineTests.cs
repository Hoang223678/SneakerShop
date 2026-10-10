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
        await harness.Bus.Publish(new OrderCreated(orderId, Guid.NewGuid(), Array.Empty<OrderLine>()));
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
        await harness.Bus.Publish(new OrderCreated(orderId, Guid.NewGuid(), Array.Empty<OrderLine>()));
        Assert.True(await harness.Consumed.Any<OrderCreated>());

        await harness.Bus.Publish(new OrderReservationTimedOut(orderId));
        Assert.True(await harness.Consumed.Any<OrderReservationTimedOut>());
        Assert.True(await harness.Published.Any<OrderCancelled>());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
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

    private static async Task SeedOrderAsync(IServiceProvider provider, Guid orderId)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Orders.Add(new Order
        {
            Id = orderId,
            UserId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            ShippingName = "Test",
            ShippingPhone = "0900000000",
            ShippingAddress = "Test address"
        });
        await db.SaveChangesAsync();
    }
}
