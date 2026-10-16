namespace Inventory.Api.Caching;

/// <summary>
/// Display-only cache of "pairs left" for product pages. Reservations never read it: they always lock
/// and check the database row. Bump <see cref="Version"/> when <c>StockAvailability</c> changes shape.
/// </summary>
public static class InventoryCacheKeys
{
    private const string Version = "v1";

    public static readonly TimeSpan AvailabilityDuration = TimeSpan.FromSeconds(5);

    public static string Availability(Guid variantId) => $"{Version}:availability:{variantId}";
}
