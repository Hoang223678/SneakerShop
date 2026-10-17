using System.Net;
using System.Net.Http.Json;

namespace Payment.Api.Services;

public interface IOrderClient
{
    Task<OrderPaymentDetails> GetPaymentDetailsAsync(Guid orderId, CancellationToken ct);
}

public sealed record OrderPaymentDetails(
    Guid OrderId,
    Guid UserId,
    string Status,
    decimal Amount,
    DateTime PaymentExpiresAt);

public sealed class OrderClient(HttpClient http) : IOrderClient
{
    public async Task<OrderPaymentDetails> GetPaymentDetailsAsync(Guid orderId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"internal/orders/{orderId}/payment-details", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException("Order not found.");
        response.EnsureSuccessStatusCode();
        var details = await response.Content.ReadFromJsonAsync<OrderPaymentDetails>(cancellationToken: ct);
        return details ?? throw new InvalidOperationException("Ordering returned an empty payment-details response.");
    }
}
