using System.ComponentModel.DataAnnotations;

namespace Ordering.Api.Dtos;

public record FlashSaleStatus(Guid VariantId, int Remaining, DateTime EndsAtUtc);

public class StartFlashSaleRequest
{
    /// <summary>Pairs offered in the sale. Set it to at most the stock reserved for the sale in Inventory.</summary>
    [Range(1, 100_000)]
    public int Quantity { get; set; }

    [Required]
    public DateTimeOffset? EndsAt { get; set; }
}
