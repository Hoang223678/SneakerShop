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
    [Range(1, 10)]
    public int Quantity { get; set; }
}
