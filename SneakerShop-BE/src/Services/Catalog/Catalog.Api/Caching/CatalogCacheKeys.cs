using System.Globalization;
using Catalog.Api.Dtos;

namespace Catalog.Api.Caching;

/// <summary>
/// Bump <see cref="Version"/> whenever a cached DTO changes shape, so entries written by the previous
/// build are never deserialized into the new type (they simply expire).
/// </summary>
public static class CatalogCacheKeys
{
    private const string Version = "v1";

    public const string SneakerListTag = "sneaker-list";

    public static readonly TimeSpan BrandsDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SneakerListDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SneakerDetailDuration = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BestSellersDuration = TimeSpan.FromMinutes(1);

    public static string Brands => $"{Version}:brands";

    public static string Sneaker(Guid id) => $"{Version}:sneaker:{id}";

    public static string SneakerList(SneakerQuery q, int page, int pageSize)
    {
        var sort = q.Sort is "price_asc" or "price_desc" or "newest" ? q.Sort : "name";
        return string.Create(CultureInfo.InvariantCulture,
            $"{Version}:sneakers:{q.Search?.Trim().ToLowerInvariant()}|{q.BrandId}|{q.Gender}|{q.MinPrice}|{q.MaxPrice}|{sort}|{page}|{pageSize}");
    }

    public static string BestSellers(int days, int take) => $"{Version}:best-sellers:{days}:{take}";
}
