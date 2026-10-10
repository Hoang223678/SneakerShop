using Ordering.Api.Domain;

namespace Ordering.Api.Dtos;

public static class OrderMapping
{
    public static OrderDto ToDto(this Order o) => new(
        o.Id, o.UserId, o.Status, o.IsPaid, o.TotalAmount,
        o.ShippingName, o.ShippingPhone, o.ShippingAddress,
        o.FailureReason, o.CreatedAt, o.UpdatedAt,
        o.Items.Select(i => new OrderItemDto(
            i.VariantId, i.SneakerId, i.SneakerName, i.Sku,
            i.Size, i.Color, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList());
}
