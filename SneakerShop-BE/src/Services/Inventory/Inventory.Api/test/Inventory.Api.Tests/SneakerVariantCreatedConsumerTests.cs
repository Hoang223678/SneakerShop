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
        var sneakerId = Guid.NewGuid();
        var context = new Mock<ConsumeContext<SneakerVariantCreated>>();
        context.SetupGet(c => c.Message).Returns(new SneakerVariantCreated(variantId, sneakerId, "AIR-ZOOM-BLACK-9.0"));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var consumer = new SneakerVariantCreatedConsumer(db);
        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);

        var stock = Assert.Single(db.StockItems);
        Assert.Equal(variantId, stock.VariantId);
        Assert.Equal(sneakerId, stock.SneakerId);
        Assert.Equal("AIR-ZOOM-BLACK-9.0", stock.Sku);
        Assert.Equal(0, stock.Quantity);
        Assert.Equal(0, stock.Reserved);
    }

    [Fact]
    public async Task UpdatedConsumer_RefreshesSku()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new InventoryDbContext(options);
        var variantId = Guid.NewGuid();
        var sneakerId = Guid.NewGuid();
        db.StockItems.Add(new Inventory.Api.Domain.StockItem
        {
            VariantId = variantId,
            SneakerId = sneakerId,
            Sku = "OLD",
            Quantity = 5
        });
        await db.SaveChangesAsync();
        var context = new Mock<ConsumeContext<SneakerVariantUpdated>>();
        context.SetupGet(c => c.Message).Returns(new SneakerVariantUpdated(variantId, sneakerId, "NEW"));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new SneakerVariantUpdatedConsumer(db).Consume(context.Object);

        Assert.Equal("NEW", Assert.Single(db.StockItems).Sku);
    }

    [Fact]
    public async Task DeletedConsumer_RemovesUnreservedStock()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new InventoryDbContext(options);
        var variantId = Guid.NewGuid();
        db.StockItems.Add(new Inventory.Api.Domain.StockItem { VariantId = variantId, Sku = "AZ-9", Quantity = 2 });
        await db.SaveChangesAsync();
        var context = new Mock<ConsumeContext<SneakerVariantDeleted>>();
        context.SetupGet(c => c.Message).Returns(new SneakerVariantDeleted(variantId));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new SneakerVariantDeletedConsumer(db).Consume(context.Object);

        Assert.Empty(db.StockItems);
    }
}
