namespace Inventory.Api.Domain;

public enum ReservationStatus
{
    Reserved,   // held for a pending/confirmed order
    Committed,  // order shipped: stock permanently deducted
    Released    // order cancelled: stock returned to available
}

/// <summary>Stock for one sneaker variant. Available = Quantity - Reserved.</summary>
public class StockItem
{
    public Guid VariantId { get; set; }          // same ID as Catalog's SneakerVariant.Id (no FK across services)
    public string? Sku { get; set; }
    public int Quantity { get; set; }            // physical pairs on hand
    public int Reserved { get; set; }            // pairs held for open orders
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>(); // optimistic concurrency

    public int Available => Quantity - Reserved;
}

/// <summary>Audit trail of what was held for which order. Also makes event handling idempotent.</summary>
public class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
