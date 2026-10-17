using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Dtos;

public record AvailabilityRequest(List<Guid> VariantIds);

public record StockAvailability(Guid VariantId, int Available, bool InStock);

public record StockItemDto(Guid VariantId, string? Sku, int Quantity, int Reserved, int Available, DateTime UpdatedAt);

public class SetStockRequest
{
    [Range(0, 1_000_000)]
    public int Quantity { get; set; }
}
