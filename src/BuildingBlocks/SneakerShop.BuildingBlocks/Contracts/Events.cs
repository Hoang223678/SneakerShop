namespace SneakerShop.Contracts;

/// <summary>One line of an order: which variant (size/color) and how many pairs.</summary>
public record OrderLine(Guid VariantId, int Quantity);

// ---- Published by Catalog ----
public record SneakerVariantCreated(Guid VariantId, Guid SneakerId, string Sku);

// ---- Published by Ordering ----
public record OrderCreated(Guid OrderId, Guid UserId, IReadOnlyList<OrderLine> Items);
public record OrderCancelled(Guid OrderId);
public record OrderShipped(Guid OrderId, IReadOnlyList<OrderLine> Items);
public record OrderPaid(Guid OrderId);
public record OrderPaymentReceived(Guid OrderId);
public record OrderReservationTimedOut(Guid OrderId);
public record OrderReservationExpired(Guid OrderId);

// ---- Published by Inventory ----
public record StockReserved(Guid OrderId);
public record StockReservationFailed(Guid OrderId, string Reason);
