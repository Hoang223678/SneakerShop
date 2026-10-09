namespace Ordering.Api.Domain;

public enum OrderStatus
{
    Pending,    // created, waiting for Inventory to reserve stock
    Confirmed,  // stock reserved
    Rejected,   // stock could not be reserved
    Cancelled,  // cancelled by customer/admin
    Shipped,
    Delivered
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }

    public string ShippingName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;

    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<OrderItem> Items { get; set; } = new();

    // ---- State machine: each transition returns false when it is not allowed from the current status ----

    public bool TryConfirm() => Transition(OrderStatus.Pending, OrderStatus.Confirmed);

    public bool TryReject(string reason)
    {
        if (!Transition(OrderStatus.Pending, OrderStatus.Rejected)) return false;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
        return true;
    }

    public bool TryCancel()
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.Confirmed)) return false;
        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public bool TryShip() => Transition(OrderStatus.Confirmed, OrderStatus.Shipped);

    public bool TryDeliver() => Transition(OrderStatus.Shipped, OrderStatus.Delivered);

    private bool Transition(OrderStatus from, OrderStatus to)
    {
        if (Status != from) return false;
        Status = to;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}

/// <summary>Snapshot of the product at purchase time, so later catalog edits never change past orders.</summary>
public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public Guid SneakerId { get; set; }
    public string SneakerName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public string Color { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
