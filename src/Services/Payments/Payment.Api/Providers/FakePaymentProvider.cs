using Payment.Api.Domain;

namespace Payment.Api.Providers;

public sealed class FakePaymentProvider : IPaymentProvider
{
    public string Name => "Fake";

    public void EnsureReady() { }

    public Task<string?> CreatePaymentAsync(PaymentAttempt attempt, PaymentRecord payment, string clientIp, CancellationToken ct) =>
        Task.FromResult<string?>(null);

    public bool TryVerifyWebhook(IReadOnlyDictionary<string, string> payload, out VerifiedPaymentWebhook webhook)
    {
        webhook = default!;
        return false;
    }

    public Task<ProviderPaymentStatus?> QueryStatusAsync(PaymentAttempt attempt, CancellationToken ct) =>
        Task.FromResult<ProviderPaymentStatus?>(null);

    public Task RefundAsync(PaymentAttempt attempt, Guid refundId, decimal amount, CancellationToken ct) =>
        Task.CompletedTask;
}
