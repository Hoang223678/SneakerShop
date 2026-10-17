using System.ComponentModel.DataAnnotations;

namespace Cart.Api.Dtos;

/// <summary>
/// One cart line. Name and price come from Catalog at read time and are for display only;
/// Ordering reads prices again when the order is placed. <c>Available</c> is false when the variant was removed.
/// </summary>
public record CartItemDto(
    Guid VariantId,
    int Quantity,
    bool Available,
    Guid? SneakerId,
    string? SneakerName,
    string? Sku,
    decimal? Size,
    string? Color,
    decimal? UnitPrice);

public record CartDto(IReadOnlyList<CartItemDto> Items, decimal Subtotal);

public class SetCartItemRequest
{
    [Range(1, 1_000_000)]
    public int Quantity { get; set; }
}

public class AddCartItemRequest
{
    [Required]
    public Guid VariantId { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }
}

public class AddCartItemsRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public List<AddCartItemRequest> Items { get; set; } = [];
}

public class CheckoutCartRequest
{
    [Required, MaxLength(150)]
    public string ShippingName { get; set; } = string.Empty;

    [Required, Phone, MaxLength(30)]
    public string ShippingPhone { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Method { get; set; } = "vnpay";
}

public record CheckoutOrderItemDto(
    Guid VariantId, Guid SneakerId, string SneakerName, string Sku,
    decimal Size, string Color, decimal UnitPrice, int Quantity, decimal LineTotal);

public record CheckoutOrderDto(
    Guid Id, Guid UserId, string Status, bool IsPaid, Guid? PaymentId, decimal TotalAmount,
    DateTime PaymentExpiresAt,
    string ShippingName, string ShippingPhone, string ShippingAddress,
    string? FailureReason, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<CheckoutOrderItemDto> Items);

public record CheckoutPaymentDto(
    Guid Id, Guid OrderId, decimal Amount, string Currency, string Status,
    DateTimeOffset ExpiresAt, Guid? AttemptId, string? PaymentUrl);

public record CartCheckoutResponse(CheckoutOrderDto Order, CheckoutPaymentDto? Payment);

public record InternalCartLineDto(Guid VariantId, int Quantity, bool Available);

public record InternalCartDto(IReadOnlyList<InternalCartLineDto> Items);
