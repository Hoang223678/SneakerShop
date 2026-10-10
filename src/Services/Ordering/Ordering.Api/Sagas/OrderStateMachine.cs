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
    public Event<OrderPaymentReceived> OrderPaymentReceived { get; private set; } = null!;

    public OrderStateMachine(IDbContextFactory<OrderDbContext> orderDbFactory)
    {
        this.orderDbFactory = orderDbFactory;
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReserved, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReservationFailed, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => ReservationTimedOut, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => ReservationExpired, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderCancelled, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderPaymentReceived, x => x.CorrelateById(context => context.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(context => context.Saga.ReservationDeadline = DateTime.UtcNow.AddMinutes(2))
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
            When(OrderPaymentReceived)
                .ThenAsync(async context =>
                {
                    await using var db = await orderDbFactory.CreateDbContextAsync(context.CancellationToken);
                    var order = await db.Orders.FirstOrDefaultAsync(
                        x => x.Id == context.Saga.CorrelationId, context.CancellationToken);
                    if (order is null || order.Status != OrderStatus.Confirmed) return;

                    if (!order.IsPaid && order.TryMarkPaid())
                        await db.SaveChangesAsync(context.CancellationToken);
                    context.Saga.PaymentAccepted = true;
                    await context.Publish(new OrderPaid(context.Saga.CorrelationId), context.CancellationToken);
                })
                .IfElse(
                    context => context.Saga.PaymentAccepted,
                    accepted => accepted.TransitionTo(Paid),
                    rejected => rejected.Then(context => context.Saga.PaymentAccepted = false)),
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
            When(OrderShipped).Finalize(),
            When(OrderCancelled).Finalize());

        SetCompletedWhenFinalized();
    }
}
