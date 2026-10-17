using SneakerShop.BuildingBlocks;

namespace Ordering.Api.Services;

public interface ICartClient
{
    Task<CartSnapshot> GetAsync(Guid userId, CancellationToken ct);
}

public sealed record CartSnapshot(IReadOnlyList<CartSnapshotItem> Items);

public sealed record CartSnapshotItem(Guid VariantId, int Quantity, bool Available);

public sealed class CartClient(HttpClient http) : ICartClient
{
    public async Task<CartSnapshot> GetAsync(Guid userId, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync($"internal/cart/{userId}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "Cart service is unavailable. Please try again.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new AppException(StatusCodes.Status502BadGateway, "Cart service returned an unexpected error.");

            return await response.Content.ReadFromJsonAsync<CartSnapshot>(ct)
                   ?? new CartSnapshot([]);
        }
    }
}
