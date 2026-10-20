using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Dtos;

public record AvailabilityRequest(List<Guid> VariantIds);

public record StockAvailability(Guid VariantId, int Available, bool InStock);

public record StockItemDto(Guid VariantId, Guid SneakerId, string? Sku, int Quantity, int Reserved, int Available, DateTime UpdatedAt);

public class SetStockRequest
{
    [Range(0, 1_000_000)]
    public int Quantity { get; set; }
}

public class CreateVariantStockRequest
{
    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Range(1, 25)]
    public decimal Size { get; set; }

    [Required, MaxLength(50)]
    public string Color { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public int Quantity { get; set; }
}

public class UpdateVariantRequest
{
    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Range(1, 25)]
    public decimal Size { get; set; }

    [Required, MaxLength(50)]
    public string Color { get; set; } = string.Empty;
}

public record VariantStockDto(
    Guid VariantId,
    Guid SneakerId,
    string Sku,
    decimal Size,
    string Color,
    int Quantity,
    int Reserved,
    int Available,
    DateTime UpdatedAt);
