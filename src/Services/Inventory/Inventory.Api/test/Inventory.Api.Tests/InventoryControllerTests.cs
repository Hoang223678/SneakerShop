using Inventory.Api.Controllers;
using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using Xunit;

namespace Inventory.Api.Tests;

public sealed class InventoryControllerTests : IDisposable
{
    private readonly InventoryDbContext _db;
    private readonly InventoryController _controller;

    public InventoryControllerTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new InventoryDbContext(options);
        _controller = new InventoryController(_db);
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

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var availability = Assert.IsType<StockAvailability>(ok.Value);
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

        var result = await _controller.List(maxAvailable: 4, page: 1, pageSize: 10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<StockItemDto>>(ok.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "A-200", "M-300" }, page.Items.Select(item => item.Sku).OrderBy(s => s).ToArray());
    }

    public void Dispose() => _db.Dispose();
}
