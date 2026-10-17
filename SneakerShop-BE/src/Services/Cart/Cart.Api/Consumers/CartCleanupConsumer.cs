using Cart.Api.Services;
using MassTransit;
using SneakerShop.Contracts;

namespace Cart.Api.Consumers;

/// <summary>Removes ordered variants from the customer's cart once the order is placed. Safe to redeliver.</summary>
public class CartCleanupConsumer(ICartStore carts) : IConsumer<OrderCreated>
{
    public Task Consume(ConsumeContext<OrderCreated> context) =>
        carts.RemoveAsync(context.Message.UserId, context.Message.Items.Select(i => i.VariantId));
}
