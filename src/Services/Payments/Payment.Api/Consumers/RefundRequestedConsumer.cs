using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.Domain;
using Payment.Api.Providers;
using SneakerShop.Contracts;

namespace Payment.Api.Consumers;

public sealed class RefundRequestedConsumer(
    PaymentDbContext db,
    IEnumerable<IPaymentProvider> providers,
    ILogger<RefundRequestedConsumer> logger) : IConsumer<RefundRequested>
{
    public async Task Consume(ConsumeContext<RefundRequested> context)
    {
        var message = context.Message;
        var payment = await db.Payments.Include(x => x.Attempts).Include(x => x.Refunds)
            .SingleOrDefaultAsync(x => x.Id == message.PaymentId && x.OrderId == message.OrderId,
                context.CancellationToken);
        if (payment is null)
        {
            logger.LogWarning("Refund requested for missing payment {PaymentId}.", message.PaymentId);
            return;
        }

        var refund = await db.PaymentRefunds.SingleOrDefaultAsync(
            x => x.Id == message.RefundId, context.CancellationToken);
        if (refund is not null &&
            (refund.PaymentId != payment.Id ||
             (!message.RefundRemaining && refund.Amount != message.Amount)))
            throw new InvalidOperationException($"Refund idempotency key {refund.Id} was reused with different refund details.");
        if (refund?.Completed == true) return;

        if (refund is null)
        {
            var pendingAmount = payment.Refunds.Where(x => !x.Completed).Sum(x => x.Amount);
            var refundAmount = message.RefundRemaining
                ? payment.Amount - payment.AmountRefunded - pendingAmount
                : message.Amount;
            if (message.RefundRemaining && refundAmount <= 0)
                return;
            if (refundAmount <= 0 || refundAmount > payment.Amount - payment.AmountRefunded - pendingAmount)
                throw new InvalidOperationException($"Invalid refund amount for payment {payment.Id}.");
            refund = new PaymentRefund { Id = message.RefundId, PaymentId = payment.Id, Amount = refundAmount };
            db.PaymentRefunds.Add(refund);
            await db.SaveChangesAsync(context.CancellationToken);
        }
        var attempt = payment.Attempts
            .Where(x => x.Status == PaymentStatus.Succeeded && !string.IsNullOrWhiteSpace(x.ProviderTransactionId))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault() ?? throw new InvalidOperationException($"No successful attempt exists for payment {payment.Id}.");
        var provider = providers.Single(x => string.Equals(x.Name, attempt.Provider, StringComparison.OrdinalIgnoreCase));
        await provider.RefundAsync(attempt, refund.Id, refund.Amount, context.CancellationToken);

        refund.Completed = true;
        payment.AmountRefunded += refund.Amount;
        if (payment.AmountRefunded == payment.Amount)
            payment.Status = PaymentStatus.Refunded;
        await context.Publish(
            new PaymentRefunded(payment.OrderId, payment.Id, refund.Amount, payment.Status == PaymentStatus.Refunded),
            context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
