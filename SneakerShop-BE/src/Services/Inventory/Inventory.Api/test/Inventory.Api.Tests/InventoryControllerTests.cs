using Inventory.Api.Controllers;
using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Dtos;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SneakerShop.BuildingBlocks;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Inventory.Api.Tests;

public sealed class InventoryControllerTests : IDisposable
{
    private readonly InventoryDbContext _db;
    private readonly InventoryController _controller;
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly Mock<ICatalogVariantClient> _catalog = new();

    public InventoryControllerTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new InventoryDbContext(options);
        _controller = new InventoryController(_db, _cache, _catalog.Object);
    }

    [Fact]
    public async Task SetStock_RefreshesCachedAvailability()
    {
        var variantId = Guid.NewGuid();
        _db.StockItems.Add(new StockItem { VariantId = variantId, Sku = "ABC-012", Quantity = 2, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        Assert.Equal(2, (await _controller.GetAvailability(variantId, CancellationToken.None)).Value!.Available);

        await _controller.SetStock(variantId, new SetStockRequest { Quantity = 7 }, CancellationToken.None);

        Assert.Equal(7, (await _controller.GetAvailability(variantId, CancellationToken.None)).Value!.Available);
    }

    [Fact]
    public async Task GetAvailabilityBatch_CombinesCachedAndFreshEntriesAndSkipsUnknownVariants()
    {
        var cachedId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        _db.StockItems.AddRange(
            new StockItem { VariantId = cachedId, Sku = "ABC-013", Quantity = 4, UpdatedAt = DateTime.UtcNow },
            new StockItem { VariantId = freshId, Sku = "ABC-014", Quantity = 1, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        await _controller.GetAvailability(cachedId, CancellationToken.None);

        var result = await _controller.GetAvailabilityBatch(
            new AvailabilityRequest([cachedId, freshId, Guid.NewGuid()]), CancellationToken.None);

        Assert.Equal(
            new[] { (cachedId, 4), (freshId, 1) },
            result.Value!.Select(a => (a.VariantId, a.Available)).ToArray());
    }

    [Fact]
    public async Task GetAvailability_ReturnsZeroWhenReservedStockExceedsQuantity()
    {
        var variantId = Guid.NewGuid();
        _db.StockItems.Add(new StockItem
        {
            VariantId = variantId,
            Sku = "ABC-010",
            Quantity = 3,
            Reserved = 5,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAvailability(variantId, CancellationToken.None);

        var availability = Assert.IsType<StockAvailability>(result.Value);
        Assert.Equal(variantId, availability.VariantId);
        Assert.Equal(0, availability.Available);
        Assert.False(availability.InStock);
    }

    [Fact]
    public async Task SetStock_RejectsQuantityLowerThanReservedPairs()
    {
        var variantId = Guid.NewGuid();
        _db.StockItems.Add(new StockItem
        {
            VariantId = variantId,
            Sku = "ABC-011",
            Quantity = 8,
            Reserved = 3,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.SetStock(variantId, new SetStockRequest { Quantity = 2 }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task List_FiltersLowStockRowsByAvailablePairs()
    {
        _db.StockItems.AddRange(
            new StockItem { VariantId = Guid.NewGuid(), Sku = "Z-100", Quantity = 10, Reserved = 6 },
            new StockItem { VariantId = Guid.NewGuid(), Sku = "A-200", Quantity = 10, Reserved = 2 },
            new StockItem { VariantId = Guid.NewGuid(), Sku = "M-300", Quantity = 4, Reserved = 1 });
        await _db.SaveChangesAsync();

        var result = await _controller.List(sneakerId: null, maxAvailable: 4, page: 1, pageSize: 10, CancellationToken.None);

        var page = Assert.IsType<PagedResult<StockItemDto>>(result.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "M-300", "Z-100" }, page.Items.Select(item => item.Sku).OrderBy(s => s).ToArray());
    }

    [Fact]
    public async Task List_FiltersRowsBySneakerId()
    {
        var sneakerA = Guid.NewGuid();
        var sneakerB = Guid.NewGuid();
        _db.StockItems.AddRange(
            new StockItem { VariantId = Guid.NewGuid(), SneakerId = sneakerA, Sku = "A-8", Quantity = 5 },
            new StockItem { VariantId = Guid.NewGuid(), SneakerId = sneakerA, Sku = "A-9", Quantity = 3 },
            new StockItem { VariantId = Guid.NewGuid(), SneakerId = sneakerB, Sku = "B-8", Quantity = 7 });
        await _db.SaveChangesAsync();

        var result = await _controller.ListBySneaker(sneakerA, page: 1, pageSize: 20, CancellationToken.None);

        var page = Assert.IsType<PagedResult<StockItemDto>>(result.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal(sneakerA, item.SneakerId));
        Assert.Equal(new[] { "A-8", "A-9" }, page.Items.Select(item => item.Sku).ToArray());
    }

    [Fact]
    public async Task CreateVariant_CreatesCatalogVariantAndStock()
    {
        var sneakerId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        _catalog.Setup(c => c.CreateAsync(sneakerId, "AZ-9", 9m, "Blue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogVariantDto(variantId, sneakerId, "AZ-9", 9m, "Blue"));

        var result = await _controller.CreateVariant(sneakerId, new CreateVariantStockRequest
        {
            Sku = "AZ-9",
            Size = 9m,
            Color = "Blue",
            Quantity = 12
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<VariantStockDto>(created.Value);
        Assert.Equal(variantId, dto.VariantId);
        Assert.Equal(sneakerId, dto.SneakerId);
        Assert.Equal(9m, dto.Size);
        Assert.Equal("Blue", dto.Color);
        Assert.Equal(12, dto.Quantity);
        var stock = Assert.Single(_db.StockItems);
        Assert.Equal(12, stock.Quantity);
    }

    [Fact]
    public async Task UpdateVariant_UpdatesSkuOnStock()
    {
        var sneakerId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        _db.StockItems.Add(new StockItem { VariantId = variantId, SneakerId = sneakerId, Sku = "OLD", Quantity = 4 });
        await _db.SaveChangesAsync();
        _catalog.Setup(c => c.UpdateAsync(variantId, "NEW-SKU", 10m, "Black", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogVariantDto(variantId, sneakerId, "NEW-SKU", 10m, "Black"));

        var result = await _controller.UpdateVariant(variantId, new UpdateVariantRequest
        {
            Sku = "NEW-SKU",
            Size = 10m,
            Color = "Black"
        }, CancellationToken.None);

        var dto = Assert.IsType<VariantStockDto>(result.Value);
        Assert.Equal("NEW-SKU", dto.Sku);
        Assert.Equal(10m, dto.Size);
        Assert.Equal("Black", dto.Color);
        Assert.Equal(4, dto.Quantity);
        Assert.Equal("NEW-SKU", (await _db.StockItems.SingleAsync()).Sku);
    }

    [Fact]
    public async Task DeleteVariant_RemovesStockAfterCatalogDelete()
    {
        var variantId = Guid.NewGuid();
        _db.StockItems.Add(new StockItem { VariantId = variantId, Sku = "AZ-9", Quantity = 3 });
        await _db.SaveChangesAsync();
        _catalog.Setup(c => c.DeleteAsync(variantId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.DeleteVariant(variantId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_db.StockItems);
    }

    [Fact]
    public async Task DeleteVariant_RejectsWhenPairsAreReserved()
    {
        var variantId = Guid.NewGuid();
        _db.StockItems.Add(new StockItem { VariantId = variantId, Sku = "AZ-9", Quantity = 3, Reserved = 1 });
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.DeleteVariant(variantId, CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        _catalog.Verify(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }
}
