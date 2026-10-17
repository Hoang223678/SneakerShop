using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using SneakerShop.Contracts;

namespace Ordering.Api.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    private readonly IDbContextFactory<OrderDbContext> orderDbFactory;

    public State AwaitingReservation { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State Paid { get; private set; } = null!;

    public Event<OrderCreated> OrderCreated { get; private set; } = null!;
    public Event<StockReserved> StockReserved { get; private set; } = null!;
    public Event<StockReservationFailed> StockReservationFailed { get; private set; } = null!;
    public Event<OrderReservationTimedOut> ReservationTimedOut { get; private set; } = null!;
    public Event<OrderReservationExpired> ReservationExpired { get; private set; } = null!;
    public Event<OrderCancelled> OrderCancelled { get; private set; } = null!;
    public Event<OrderShipped> OrderShipped { get; private set; } = null!;
    public Event<PaymentSucceeded> PaymentSucceeded { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Event<PaymentRefunded> PaymentRefunded { get; private set; } = null!;

    public OrderStateMachine(IDbContextFactory<OrderDbContext> orderDbFactory)
    {
        this.orderDbFactory = orderDbFactory;
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReserved, x =>
        {
            x.CorrelateById(context => context.Message.OrderId);
            x.OnMissingInstance(m => m.ExecuteAsync(context =>
                RepublishUntilSagaExists(context, context.Message)));
        });
        Event(() => StockReservationFailed, x =>
        {
            x.CorrelateById(context => context.Message.OrderId);
            x.OnMissingInstance(m => m.ExecuteAsync(context =>
                RepublishUntilSagaExists(context, context.Message)));
        });
        Event(() => ReservationTimedOut, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => ReservationExpired, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderCancelled, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentSucceeded, x =>
        {
            x.CorrelateById(context => context.Message.OrderId);
            x.OnMissingInstance(m => m.ExecuteAsync(context =>
                context.Publish(new RefundRequested(
                    context.Message.OrderId,
                    context.Message.OrderId,
                    context.Message.PaymentId,
                    0,
                    RefundRemaining: true))));
        });
        Event(() => PaymentFailed, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentRefunded, x => x.CorrelateById(context => context.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(context => context.Saga.ReservationDeadline = context.Message.PaymentExpiresAt)
                .TransitionTo(AwaitingReservation));

        During(AwaitingReservation,
            Ignore(OrderCreated),
            When(StockReserved)
                .ThenAsync(async context =>
                {
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is null) return;

                    if (order.Status == OrderStatus.Cancelled)
                    {
                        await context.Publish(new OrderCancelled(order.Id), context.CancellationToken);
                        return;
                    }

                    if (order.TryConfirm())
                        await db.SaveChangesAsync(context.CancellationToken);
                })
                .TransitionTo(AwaitingPayment),
            When(StockReservationFailed)
                .ThenAsync(async context =>
                {
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is not null && order.TryReject(context.Message.Reason))
                        await db.SaveChangesAsync(context.CancellationToken);
                })
                .Finalize(),
            When(ReservationTimedOut)
                .ThenAsync(async context =>
                {
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is not null && order.TryCancel())
                        await db.SaveChangesAsync(context.CancellationToken);
                    await context.Publish(new OrderCancelled(context.Saga.CorrelationId), context.CancellationToken);
                })
                .Finalize(),
            When(OrderCancelled).Finalize());

        During(AwaitingPayment,
            When(PaymentSucceeded)
                .ThenAsync(async context =>
                {
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is null || order.Status != OrderStatus.Confirmed ||
                        order.PaymentExpiresAt <= DateTime.UtcNow ||
                        context.Message.Amount != order.TotalAmount ||
                        context.Message.Amount <= 0)
                    {
                        await context.Publish(
                            new RefundRequested(
                                context.Message.OrderId,
                                context.Message.OrderId,
                                context.Message.PaymentId,
                                0,
                                RefundRemaining: true),
                            context.CancellationToken);
                        return;
                    }

                    if (!order.IsPaid && order.TryMarkPaid())
                        order.PaymentId = context.Message.PaymentId;

                    var shipped = order.TryShip();
                    await db.SaveChangesAsync(context.CancellationToken);
                    context.Saga.PaymentAccepted = true;
                    await context.Publish(new OrderPaid(context.Saga.CorrelationId), context.CancellationToken);
                    if (shipped)
                    {
                        var lines = order.Items.Select(i => new OrderLine(i.VariantId, i.Quantity)).ToList();
                        await context.Publish(new OrderShipped(order.Id, lines), context.CancellationToken);
                    }
                })
                .IfElse(
                    context => context.Saga.PaymentAccepted,
                    accepted => accepted.TransitionTo(Paid),
                    rejected => rejected.Then(context => context.Saga.PaymentAccepted = false)),
            When(PaymentFailed)
                .Then(context => context.Saga.PaymentAccepted = false),
            When(PaymentRefunded)
                .ThenAsync(async context =>
                {
                    if (!context.Message.FullyRefunded) return;
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is not null && order.Status == OrderStatus.Confirmed && order.TryCancel())
                    {
                        await db.SaveChangesAsync(context.CancellationToken);
                        await context.Publish(new OrderCancelled(order.Id), context.CancellationToken);
                    }
                })
                .IfElse(
                    context => context.Message.FullyRefunded,
                    refunded => refunded.Finalize(),
                    partial => partial.Then(context => context.Saga.PaymentAccepted = false)),
            When(ReservationExpired)
                .ThenAsync(async context =>
                {
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is not null && order.TryCancel())
                        await db.SaveChangesAsync(context.CancellationToken);
                    await context.Publish(new OrderCancelled(context.Saga.CorrelationId), context.CancellationToken);
                })
                .Finalize(),
            When(OrderCancelled).Finalize());

        During(Paid,
            Ignore(ReservationExpired),
            When(PaymentRefunded)
                .ThenAsync(async context =>
                {
                    if (!context.Message.FullyRefunded) return;
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is not null && order.Status == OrderStatus.Confirmed && order.TryCancel())
                    {
                        await db.SaveChangesAsync(context.CancellationToken);
                        await context.Publish(new OrderCancelled(order.Id), context.CancellationToken);
                    }
                }),
            When(OrderShipped).Finalize(),
            When(OrderCancelled).Finalize());

        SetCompletedWhenFinalized();
    }

    // Inventory can reserve and publish StockReserved before this saga has processed OrderCreated.
    // Re-publish a few times so the confirmation is not dropped.
    private static async Task RepublishUntilSagaExists<T>(ConsumeContext<T> context, T message)
        where T : class
    {
        var attempt = context.Headers.Get<string>("x-missing-saga-retry") is { } raw
            && int.TryParse(raw, out var parsed)
            ? parsed
            : 0;
        if (attempt >= 15)
            return;

        await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), context.CancellationToken);
        await context.Publish(message, pipe => pipe.Headers.Set("x-missing-saga-retry", (attempt + 1).ToString()));
    }
}
