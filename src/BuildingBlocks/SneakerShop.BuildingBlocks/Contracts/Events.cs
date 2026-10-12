namespace SneakerShop.Contracts;

/// <summary>One line of an order: which variant (size/color) and how many pairs.</summary>
public record OrderLine(Guid VariantId, int Quantity);

// ---- Published by Catalog ----
public record SneakerVariantCreated(Guid VariantId, Guid SneakerId, string Sku);

// ---- Published by Ordering ----
public record OrderCreated(Guid OrderId, Guid UserId, IReadOnlyList<OrderLine> Items, DateTime PaymentExpiresAt);
public record OrderCancelled(Guid OrderId);
public record OrderShipped(Guid OrderId, IReadOnlyList<OrderLine> Items);
public record OrderPaid(Guid OrderId);
public record PaymentSucceeded(Guid OrderId, Guid PaymentId, decimal Amount);
public record PaymentFailed(Guid OrderId, Guid PaymentId, decimal Amount, string Reason);
public record RefundRequested(
    Guid RefundId, Guid OrderId, Guid PaymentId, decimal Amount, bool RefundRemaining = false);
public record PaymentRefunded(Guid OrderId, Guid PaymentId, decimal Amount, bool FullyRefunded);
public record OrderReservationTimedOut(Guid OrderId);
public record OrderReservationExpired(Guid OrderId);

// ---- Published by Inventory ----
public record StockReserved(Guid OrderId);
public record StockReservationFailed(Guid OrderId, string Reason);
