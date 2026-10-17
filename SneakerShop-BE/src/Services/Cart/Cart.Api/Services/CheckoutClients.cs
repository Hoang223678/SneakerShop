using System.Net.Http.Json;
using System.Text.Json;
using Cart.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using SneakerShop.BuildingBlocks;

namespace Cart.Api.Services;

public interface IOrderingClient
{
    Task<CheckoutOrderDto> CreateFromCartAsync(CheckoutCartRequest request, string idempotencyKey, CancellationToken ct);
    Task<CheckoutOrderDto> GetAsync(Guid orderId, CancellationToken ct);
}

public interface IPaymentClient
{
    Task<CheckoutPaymentDto> CreateAsync(Guid orderId, string method, CancellationToken ct);
}

public sealed class BearerForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return base.SendAsync(request, ct);
    }
}

public sealed class OrderingClient(HttpClient http) : IOrderingClient
{
    public async Task<CheckoutOrderDto> CreateFromCartAsync(
        CheckoutCartRequest request, string idempotencyKey, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/orders/from-cart")
        {
            Content = JsonContent.Create(new
            {
                request.ShippingName,
                request.ShippingPhone,
                request.ShippingAddress
            })
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        using var response = await SendAsync(message, "Ordering service is unavailable. Please try again.", ct);
        return await ReadAsync<CheckoutOrderDto>(response, "Ordering service returned an unexpected error.", ct);
    }

    public async Task<CheckoutOrderDto> GetAsync(Guid orderId, CancellationToken ct)
    {
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/orders/{orderId}"),
            "Ordering service is unavailable. Please try again.",
            ct);
        return await ReadAsync<CheckoutOrderDto>(response, "Ordering service returned an unexpected error.", ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, string unavailable, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, unavailable);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string fallback, CancellationToken ct)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>(ct)
                       ?? throw new AppException(StatusCodes.Status502BadGateway, fallback);
            throw await Downstream.ToAppException(response, fallback, ct);
        }
    }
}

public sealed class PaymentClient(HttpClient http) : IPaymentClient
{
    public async Task<CheckoutPaymentDto> CreateAsync(Guid orderId, string method, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/payments", new { orderId, method }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "Payment service is unavailable. Please try again.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<CheckoutPaymentDto>(ct)
                       ?? throw new AppException(StatusCodes.Status502BadGateway, "Payment service returned an unexpected error.");
            throw await Downstream.ToAppException(response, "Payment service returned an unexpected error.", ct);
        }
    }
}

internal static class Downstream
{
    public static async Task<AppException> ToAppException(
        HttpResponseMessage response, string fallback, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return new AppException((int)response.StatusCode, problem.Title);
        }
        catch (JsonException)
        {
        }

        return new AppException((int)response.StatusCode, fallback);
    }
}
