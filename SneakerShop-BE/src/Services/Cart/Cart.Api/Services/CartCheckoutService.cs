using Cart.Api.Dtos;
using SneakerShop.BuildingBlocks;

namespace Cart.Api.Services;

public interface ICartCheckoutService
{
    Task<CartCheckoutResponse> CheckoutAsync(
        Guid userId, CheckoutCartRequest request, string idempotencyKey, CancellationToken ct);
}

public sealed class CartCheckoutService(ICartStore carts, IOrderingClient orders, IPaymentClient payments)
    : ICartCheckoutService
{
    private static readonly TimeSpan ConfirmWait = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ConfirmPoll = TimeSpan.FromMilliseconds(250);

    public async Task<CartCheckoutResponse> CheckoutAsync(
        Guid userId, CheckoutCartRequest request, string idempotencyKey, CancellationToken ct)
    {
        var lines = await carts.GetAsync(userId);
        if (lines.Count == 0)
            throw new AppException(StatusCodes.Status400BadRequest, "The cart is empty.");

        var order = await orders.CreateFromCartAsync(request, idempotencyKey, ct);
        order = await WaitForDecisionAsync(order, ct);

        if (string.Equals(order.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            throw new AppException(StatusCodes.Status409Conflict,
                order.FailureReason ?? "The order was rejected because stock is unavailable.");

        if (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return new CartCheckoutResponse(order, null);

        var payment = await payments.CreateAsync(order.Id, request.Method, ct);
        return new CartCheckoutResponse(order, payment);
    }

    private async Task<CheckoutOrderDto> WaitForDecisionAsync(CheckoutOrderDto order, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + ConfirmWait;
        while (string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase)
               && DateTime.UtcNow < deadline)
        {
            await Task.Delay(ConfirmPoll, ct);
            order = await orders.GetAsync(order.Id, ct);
        }

        return order;
    }
}
