using System.ComponentModel.DataAnnotations;
using Ordering.Api.Domain;

namespace Ordering.Api.Dtos;

public class CreateOrderRequest
{
    [Required, MaxLength(150)]
    public string ShippingName { get; set; } = string.Empty;

    [Required, Phone, MaxLength(30)]
    public string ShippingPhone { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(20)]
    public List<CreateOrderItem> Items { get; set; } = new();
}

public class CreateOrderItem
{
    [Required]
    public Guid VariantId { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; }
}

public class UpdateOrderStatusRequest
{
    /// <summary>Only Shipped and Delivered can be set manually; other changes happen through events or cancel.</summary>
    [Required]
    public OrderStatus Status { get; set; }
}

public record OrderItemDto(
    Guid VariantId, Guid SneakerId, string SneakerName, string Sku,
    decimal Size, string Color, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderDto(
    Guid Id, Guid UserId, OrderStatus Status, decimal TotalAmount,
    string ShippingName, string ShippingPhone, string ShippingAddress,
    string? FailureReason, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);
