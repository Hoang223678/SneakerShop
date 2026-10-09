using System.Security.Claims;
using Catalog.Api.Controllers;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.Dtos;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;
using Xunit;

namespace Catalog.Api.Tests;

public sealed class CatalogControllerTests : IDisposable
{
    private readonly CatalogDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisher = new();
    private readonly Brand _brand;

    public CatalogControllerTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CatalogDbContext(options);
        _brand = new Brand { Name = "Nike", Slug = "nike" };
        _db.Brands.Add(_brand);
        _db.SaveChanges();
    }

    [Fact]
    public async Task ListBrands_ReturnsBrandsSortedByName()
    {
        _db.Brands.Add(new Brand { Name = "Adidas", Slug = "adidas" });
        await _db.SaveChangesAsync();

        var result = await new BrandsController(_db).List(default);

        Assert.Equal(new[] { "Adidas", "Nike" }, result.Value!.Select(brand => brand.Name));
    }

    [Fact]
    public void WriteEndpoints_RequireAdminRole()
    {
        AssertAdminRole(typeof(BrandsController), nameof(BrandsController.Create));
        AssertAdminRole(typeof(SneakersController), nameof(SneakersController.Create));
        AssertAdminRole(typeof(SneakersController), nameof(SneakersController.Update));
        AssertAdminRole(typeof(SneakersController), nameof(SneakersController.Delete));
        AssertAdminRole(typeof(SneakersController), nameof(SneakersController.AddVariant));
    }

    [Fact]
    public void InternalVariantLookup_RequiresInternalApiKey()
    {
        Assert.Contains(typeof(InternalApiKeyAttribute),
            typeof(InternalVariantsController).GetCustomAttributes(inherit: true).Select(attribute => attribute.GetType()));
    }

    [Fact]
    public async Task CreateBrand_TrimsNameAndBuildsSlug()
    {
        var result = await new BrandsController(_db).Create(new CreateBrandRequest { Name = "  New Balance  " }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var brand = Assert.IsType<BrandDto>(created.Value);
        Assert.Equal("New Balance", brand.Name);
        Assert.Equal("new-balance", brand.Slug);
    }

    [Fact]
    public async Task CreateBrand_RejectsDuplicateName()
    {
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            new BrandsController(_db).Create(new CreateBrandRequest { Name = " Nike " }, default));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task ListSneakers_FiltersSortsAndPaginatesActiveProducts()
    {
        await AddSneaker("Air Zoom", 120m);
        await AddSneaker("Court Legacy", 80m);
        await AddSneaker("Inactive shoe", 10m, active: false);

        var result = await Sneakers().List(new SneakerQuery
        {
            MinPrice = 70m,
            Sort = "price_asc",
            Page = 1,
            PageSize = 1
        }, default);

        var page = Assert.IsType<PagedResult<SneakerListItem>>(result.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("Court Legacy", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task GetSneaker_ReturnsNotFoundForMissingSneaker()
    {
        var result = await Sneakers().Get(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSneaker_HidesInactiveSneakerFromNonAdmin()
    {
        var sneaker = await AddSneaker("Retired shoe", 90m, active: false);

        var result = await Sneakers(isAdmin: false).Get(sneaker.Id, default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSneaker_AllowsAdminToReadInactiveSneaker()
    {
        var sneaker = await AddSneaker("Retired shoe", 90m, active: false);

        var result = await Sneakers(isAdmin: true).Get(sneaker.Id, default);

        Assert.Equal(sneaker.Id, Assert.IsType<SneakerDetail>(result.Value).Id);
    }

    [Fact]
    public async Task CreateSneaker_CreatesSlugAndReturnsDetails()
    {
        var result = await Sneakers().Create(new SneakerUpsertRequest
        {
            Name = " Air Zoom ",
            BrandId = _brand.Id,
            Gender = Gender.Men,
            Price = 120m
        }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var detail = Assert.IsType<SneakerDetail>(created.Value);
        Assert.Equal("air-zoom", detail.Slug);
        Assert.Equal("Nike", detail.Brand.Name);
        Assert.Equal(Gender.Men, detail.Gender);
    }

    [Fact]
    public async Task CreateSneaker_RejectsUnknownBrand()
    {
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            Sneakers().Create(new SneakerUpsertRequest
            {
                Name = "Air Zoom",
                BrandId = int.MaxValue,
                Price = 120m
            }, default));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task UpdateSneaker_ReturnsNotFoundForMissingSneaker()
    {
        var result = await Sneakers().Update(Guid.NewGuid(), new SneakerUpsertRequest(), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateSneaker_UpdatesProductDetails()
    {
        var sneaker = await AddSneaker("Old name", 75m);

        var result = await Sneakers().Update(sneaker.Id, new SneakerUpsertRequest
        {
            Name = "Updated name",
            BrandId = _brand.Id,
            Gender = Gender.Women,
            Price = 95m,
            Description = "Updated description"
        }, default);

        var updated = Assert.IsType<SneakerDetail>(result.Value);
        Assert.Equal("updated-name", updated.Slug);
        Assert.Equal(95m, updated.Price);
        Assert.Equal("Updated description", updated.Description);
    }

    [Fact]
    public async Task DeleteSneaker_SoftDeletesProduct()
    {
        var sneaker = await AddSneaker("Retired shoe", 90m);

        var result = await Sneakers().Delete(sneaker.Id, default);

        Assert.IsType<NoContentResult>(result);
        Assert.False((await _db.Sneakers.SingleAsync(s => s.Id == sneaker.Id)).IsActive);
    }

    [Fact]
    public async Task DeleteSneaker_ReturnsNotFoundForMissingSneaker()
    {
        var result = await Sneakers().Delete(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddVariant_CreatesVariantAndPublishesEvent()
    {
        var sneaker = await AddSneaker("Air Zoom", 120m);
        _publisher
            .Setup(publisher => publisher.Publish(It.IsAny<SneakerVariantCreated>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Sneakers().AddVariant(sneaker.Id, new CreateVariantRequest
        {
            Sku = " az-900 ",
            Size = 9.5m,
            Color = " Blue "
        }, default);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var variant = Assert.IsType<VariantDto>(created.Value);
        Assert.Equal("AZ-900", variant.Sku);
        Assert.Equal("Blue", variant.Color);
        _publisher.Verify(publisher => publisher.Publish(
            It.Is<SneakerVariantCreated>(message =>
                message.VariantId == variant.Id && message.SneakerId == sneaker.Id && message.Sku == "AZ-900"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddVariant_ReturnsNotFoundForMissingSneaker()
    {
        var result = await Sneakers().AddVariant(Guid.NewGuid(), new CreateVariantRequest(), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task LookupVariants_DeduplicatesIdsAndOmitsInactiveSneakers()
    {
        var active = await AddSneaker("Air Zoom", 120m);
        var inactive = await AddSneaker("Retired shoe", 90m, active: false);
        var activeVariant = new SneakerVariant { SneakerId = active.Id, Sneaker = active, Sku = "AZ-900", Size = 9.5m, Color = "Blue" };
        var inactiveVariant = new SneakerVariant { SneakerId = inactive.Id, Sneaker = inactive, Sku = "RS-900", Size = 9m, Color = "Black" };
        _db.Variants.AddRange(activeVariant, inactiveVariant);
        await _db.SaveChangesAsync();

        var result = await new InternalVariantsController(_db).Lookup(
            new VariantLookupRequest([activeVariant.Id, activeVariant.Id, inactiveVariant.Id]),
            default);

        var item = Assert.Single(result.Value!);
        Assert.Equal(activeVariant.Id, item.VariantId);
        Assert.Equal(120m, item.UnitPrice);
    }

    private SneakersController Sneakers(bool isAdmin = true)
    {
        var role = isAdmin ? Roles.Admin : Roles.Customer;
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test");
        return new SneakersController(_db, _publisher.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private async Task<Sneaker> AddSneaker(string name, decimal price, bool active = true)
    {
        var sneaker = new Sneaker
        {
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            BrandId = _brand.Id,
            Brand = _brand,
            Price = price,
            IsActive = active
        };
        _db.Sneakers.Add(sneaker);
        await _db.SaveChangesAsync();
        return sneaker;
    }

    private static void AssertAdminRole(Type controller, string action)
    {
        var authorize = controller.GetMethod(action)!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal(Roles.Admin, authorize.Roles);
    }

    public void Dispose() => _db.Dispose();
}
