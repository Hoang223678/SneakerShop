using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.Domain;
using Payment.Api.Dtos;
using Payment.Api.Providers;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Payment.Api.Services;

public sealed class PaymentService(
    PaymentDbContext db,
    IOrderClient orders,
    IEnumerable<IPaymentProvider> providers,
    IPublishEndpoint publisher,
    IWebHostEnvironment environment,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResponse> CreateAsync(
        Guid orderId, Guid userId, string method, string clientIp, CancellationToken ct)
    {
        OrderPaymentDetails order;
        try
        {
            order = await orders.GetPaymentDetailsAsync(orderId, ct);
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("Ordering has no payment details for order {OrderId}.", orderId);
            throw new AppException(StatusCodes.Status404NotFound, "Order not found.");
        }

        logger.LogInformation(
            "Ordering returned order {OrderId} status {Status} owner {OrderUserId} for caller {CallerUserId}.",
            order.OrderId, order.Status, order.UserId, userId);

        if (order.UserId != userId)
        {
            logger.LogWarning(
                "Payment caller {CallerUserId} is not the owner {OrderUserId} of order {OrderId}.",
                userId, order.UserId, orderId);
            throw new AppException(
                StatusCodes.Status404NotFound,
                environment.IsDevelopment()
                    ? "Order not found for this user. Sign in as the customer who placed the order."
                    : "Order not found.");
        }
        if (!string.Equals(order.Status, "Confirmed", StringComparison.Ordinal))
            throw new AppException(StatusCodes.Status409Conflict, "Only a confirmed order can be paid.");
        if (order.Amount <= 0 || order.PaymentExpiresAt <= DateTime.UtcNow)
            throw new AppException(StatusCodes.Status409Conflict, "The payment window for this order has expired.");

        var provider = ResolveProvider(method);
        if (provider.Name == "Fake" && !environment.IsDevelopment())
            throw new AppException(StatusCodes.Status400BadRequest, "Fake payment is available only in development.");
        try
        {
            provider.EnsureReady();
        }
        catch (InvalidOperationException exception)
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, exception.Message);
        }

        var payment = await db.Payments.Include(x => x.Attempts)
            .SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (payment is null)
        {
            payment = new PaymentRecord
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                Amount = order.Amount,
                ExpiresAt = order.PaymentExpiresAt
            };
            db.Payments.Add(payment);
        }
        else if (payment.Amount != order.Amount || payment.UserId != userId)
        {
            throw new AppException(StatusCodes.Status409Conflict, "Stored payment details do not match the order.");
        }

        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded)
            throw new AppException(StatusCodes.Status409Conflict, "This order has already been paid.");
        if (payment.Status == PaymentStatus.Expired || payment.ExpiresAt <= DateTime.UtcNow)
            throw new AppException(StatusCodes.Status409Conflict, "The payment window for this order has expired.");

        var pendingAttempt = payment.Attempts
            .Where(x => x.Status == PaymentStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        if (pendingAttempt is not null)
        {
            if (!string.Equals(pendingAttempt.Provider, provider.Name, StringComparison.OrdinalIgnoreCase))
                throw new AppException(StatusCodes.Status409Conflict, "A different payment method is already pending.");
            if (pendingAttempt.PaymentUrl is null && provider.Name == "VnPay")
            {
                try
                {
                    pendingAttempt.PaymentUrl = await provider.CreatePaymentAsync(pendingAttempt, payment, clientIp, ct);
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Retrying VNPay link creation failed for payment {PaymentId}.", payment.Id);
                    throw;
                }
            }
            return ToResponse(payment, pendingAttempt);
        }

        payment.Status = PaymentStatus.Pending;
        var attempt = new PaymentAttempt
        {
            PaymentId = payment.Id,
            Provider = provider.Name,
            ExpiresAt = payment.ExpiresAt
        };
        payment.Attempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        try
        {
            attempt.PaymentUrl = await provider.CreatePaymentAsync(attempt, payment, clientIp, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Payment link creation failed for payment {PaymentId}.", payment.Id);
            attempt.Status = payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(ct);
            throw new AppException(StatusCodes.Status502BadGateway, "The payment provider could not create a payment link.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Payment link creation failed for payment {PaymentId}.", payment.Id);
            attempt.Status = payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(ct);
            throw;
        }

        return ToResponse(payment, attempt);
    }

    public async Task<PaymentResponse> GetAsync(Guid paymentId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().Include(x => x.Attempts)
            .SingleOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null || (!isAdmin && payment.UserId != userId))
            throw new AppException(StatusCodes.Status404NotFound, "Payment not found.");
        return ToResponse(payment, payment.Attempts.OrderByDescending(x => x.CreatedAt).FirstOrDefault());
    }

    public async Task<PaymentResponse> RequestRefundAsync(
        Guid paymentId, Guid refundId, decimal amount, CancellationToken ct)
    {
        if (refundId == Guid.Empty || refundId == paymentId || amount <= 0)
            throw new AppException(StatusCodes.Status400BadRequest, "A valid idempotency key and positive refund amount are required.");

        var payment = await db.Payments.Include(x => x.Attempts).Include(x => x.Refunds)
            .SingleOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null)
            throw new AppException(StatusCodes.Status404NotFound, "Payment not found.");
        if (refundId == payment.OrderId)
            throw new AppException(StatusCodes.Status400BadRequest, "The refund idempotency key conflicts with the order refund key.");
        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.Refunded))
            throw new AppException(StatusCodes.Status409Conflict, "Only a successful payment can be refunded.");

        var existing = payment.Refunds.SingleOrDefault(x => x.Id == refundId);
        if (existing is not null)
        {
            if (existing.Amount != amount)
                throw new AppException(StatusCodes.Status409Conflict, "The idempotency key was already used for a different refund.");
            return ToResponse(payment, payment.Attempts.OrderByDescending(x => x.CreatedAt).FirstOrDefault());
        }

        var pendingRefunds = payment.Refunds.Where(x => !x.Completed).Sum(x => x.Amount);
        if (amount > payment.Amount - payment.AmountRefunded - pendingRefunds)
            throw new AppException(StatusCodes.Status409Conflict, "The refund exceeds the remaining refundable amount.");

        db.PaymentRefunds.Add(new PaymentRefund
        {
            Id = refundId,
            PaymentId = payment.Id,
            Amount = amount
        });
        await publisher.Publish(new RefundRequested(refundId, payment.OrderId, payment.Id, amount), ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(payment, payment.Attempts.OrderByDescending(x => x.CreatedAt).FirstOrDefault());
    }

    public async Task ProcessWebhookAsync(
        string providerName, IReadOnlyDictionary<string, string> payload, CancellationToken ct)
    {
        var provider = ResolveProvider(providerName);
        if (payload.Sum(pair => pair.Key.Length + pair.Value.Length) > 16_384)
            throw new AppException(StatusCodes.Status400BadRequest, "Payment provider payload is too large.");
        if (!provider.TryVerifyWebhook(payload, out var receipt))
            throw new AppException(StatusCodes.Status400BadRequest, "Invalid payment provider signature or payload.");

        var attempt = await db.PaymentAttempts.Include(x => x.Payment)
            .SingleOrDefaultAsync(x => x.Id == receipt.AttemptId && x.Provider == provider.Name, ct);
        if (attempt is null)
            throw new AppException(StatusCodes.Status404NotFound, "Payment attempt not found.");
        if (receipt.Amount != attempt.Payment.Amount)
            throw new AppException(StatusCodes.Status400BadRequest, "Payment amount does not match the order.");
        if (await db.PaymentAttempts.AnyAsync(
                x => x.Provider == provider.Name &&
                     x.ProviderTransactionId == receipt.TransactionId &&
                     x.Id != attempt.Id,
                ct))
            throw new AppException(StatusCodes.Status409Conflict, "Provider transaction has already been used.");

        logger.LogInformation(
            "Verified {Provider} webhook for order {OrderId}, attempt {AttemptId}, transaction {TransactionId}, success {Succeeded}.",
            provider.Name, attempt.Payment.OrderId, attempt.Id, receipt.TransactionId, receipt.Succeeded);

        await ApplyResultAsync(attempt, receipt.TransactionId, receipt.Succeeded,
            JsonSerializer.Serialize(payload), ct);
    }

    public async Task SimulateSuccessAsync(Guid attemptId, Guid userId, CancellationToken ct)
    {
        if (!environment.IsDevelopment())
            throw new AppException(StatusCodes.Status404NotFound, "Not found.");
        var attempt = await db.PaymentAttempts.Include(x => x.Payment)
            .SingleOrDefaultAsync(x => x.Id == attemptId && x.Provider == "Fake", ct);
        if (attempt is null || attempt.Payment.UserId != userId)
            throw new AppException(StatusCodes.Status404NotFound, "Payment attempt not found.");
        await ApplyResultAsync(attempt, $"fake-{attempt.Id:N}", true, "{\"simulated\":true}", ct);
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        var pending = await db.PaymentAttempts.Include(x => x.Payment)
            .Where(x => x.Status == PaymentStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var attempt in pending)
        {
            ct.ThrowIfCancellationRequested();
            var provider = ResolveProvider(attempt.Provider);
            if (attempt.ExpiresAt <= DateTime.UtcNow)
            {
                var status = await provider.QueryStatusAsync(attempt, ct);
                if (status is not null)
                {
                    if (status.Amount != attempt.Payment.Amount)
                        throw new InvalidOperationException(
                            $"Provider reconciliation amount does not match payment {attempt.Payment.Id}.");
                    await ApplyResultAsync(attempt, status.TransactionId, status.Succeeded, null, ct);
                }
                else
                {
                    attempt.Status = PaymentStatus.Expired;
                    if (!attempt.Payment.Attempts.Any(x => x.Status == PaymentStatus.Pending && x.Id != attempt.Id))
                        attempt.Payment.Status = PaymentStatus.Expired;
                    await db.SaveChangesAsync(ct);
                }
            }
            else
            {
                var status = await provider.QueryStatusAsync(attempt, ct);
                if (status is not null)
                {
                    if (status.Amount != attempt.Payment.Amount)
                        throw new InvalidOperationException(
                            $"Provider reconciliation amount does not match payment {attempt.Payment.Id}.");
                    await ApplyResultAsync(attempt, status.TransactionId, status.Succeeded, null, ct);
                }
            }
        }
    }

    private async Task ApplyResultAsync(
        PaymentAttempt attempt, string transactionId, bool succeeded, string? rawPayload, CancellationToken ct)
    {
        var payment = attempt.Payment;
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded)
            return;
        if (!succeeded && attempt.Status == PaymentStatus.Failed)
            return;
        attempt.ProviderTransactionId = transactionId;
        attempt.RawPayload = rawPayload;
        var amountMatches = attempt.Payment.Amount > 0;
        if (succeeded && amountMatches)
        {
            payment.Status = attempt.Status = PaymentStatus.Succeeded;
            await publisher.Publish(new PaymentSucceeded(payment.OrderId, payment.Id, payment.Amount), ct);
        }
        else
        {
            attempt.Status = PaymentStatus.Failed;
            var anotherPendingAttempt = await db.PaymentAttempts.AnyAsync(
                x => x.PaymentId == payment.Id && x.Id != attempt.Id && x.Status == PaymentStatus.Pending, ct);
            if (!anotherPendingAttempt)
                payment.Status = PaymentStatus.Failed;
            await publisher.Publish(new PaymentFailed(payment.OrderId, payment.Id, payment.Amount, "Provider reported a failed transaction."), ct);
        }
        await db.SaveChangesAsync(ct);
    }

    private IPaymentProvider ResolveProvider(string name) =>
        providers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new AppException(StatusCodes.Status400BadRequest, "Unsupported payment method.");

    private static PaymentResponse ToResponse(PaymentRecord payment, PaymentAttempt? attempt) =>
        new(payment.Id, payment.OrderId, payment.Amount, payment.Currency, payment.Status,
            new DateTimeOffset(DateTime.SpecifyKind(payment.ExpiresAt, DateTimeKind.Utc)),
            attempt?.Id, attempt?.PaymentUrl);
}
