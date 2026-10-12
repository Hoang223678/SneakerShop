using Payment.Api.Dtos;

namespace Payment.Api.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(Guid orderId, Guid userId, string method, string clientIp, CancellationToken ct);
    Task<PaymentResponse> GetAsync(Guid paymentId, Guid userId, bool isAdmin, CancellationToken ct);
    Task<PaymentResponse> RequestRefundAsync(Guid paymentId, Guid refundId, decimal amount, CancellationToken ct);
    Task ProcessWebhookAsync(string providerName, IReadOnlyDictionary<string, string> payload, CancellationToken ct);
    Task SimulateSuccessAsync(Guid attemptId, Guid userId, CancellationToken ct);
    Task ReconcileAsync(CancellationToken ct);
}
