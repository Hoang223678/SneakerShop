using Inventory.Api.Consumers;
using Inventory.Api.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using SneakerShop.Contracts;
using Xunit;

namespace Inventory.Api.Tests;

public sealed class SneakerVariantCreatedConsumerTests
{
    [Fact]
    public async Task Consume_CreatesEmptyStockForTheVariantAndIgnoresRedelivery()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new InventoryDbContext(options);
        var variantId = Guid.NewGuid();
        var context = new Mock<ConsumeContext<SneakerVariantCreated>>();
        context.SetupGet(c => c.Message).Returns(new SneakerVariantCreated(variantId, Guid.NewGuid(), "AIR-ZOOM-BLACK-9.0"));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var consumer = new SneakerVariantCreatedConsumer(db);
        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);

        var stock = Assert.Single(db.StockItems);
        Assert.Equal(variantId, stock.VariantId);
        Assert.Equal("AIR-ZOOM-BLACK-9.0", stock.Sku);
        Assert.Equal(0, stock.Quantity);
        Assert.Equal(0, stock.Reserved);
    }
}
