using Payment.Api.Domain;

namespace Payment.Api.Providers;

public interface IPaymentProvider
{
    string Name { get; }
    Task<string?> CreatePaymentAsync(PaymentAttempt attempt, PaymentRecord payment, string clientIp, CancellationToken ct);
    bool TryVerifyWebhook(IReadOnlyDictionary<string, string> payload, out VerifiedPaymentWebhook webhook);
    Task<ProviderPaymentStatus?> QueryStatusAsync(PaymentAttempt attempt, CancellationToken ct);
    Task RefundAsync(PaymentAttempt attempt, Guid refundId, decimal amount, CancellationToken ct);
}

public sealed record VerifiedPaymentWebhook(
    Guid AttemptId,
    string TransactionId,
    decimal Amount,
    bool Succeeded);

public sealed record ProviderPaymentStatus(string TransactionId, decimal Amount, bool Succeeded);
