using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;

namespace Cart.Api.Services;

public interface ICatalogClient
{
    Task<IReadOnlyList<VariantLookupItem>> LookupAsync(IReadOnlyList<Guid> variantIds, CancellationToken ct);
}

/// <summary>Typed HttpClient for the Catalog service's internal endpoint (see Program.cs for base URL and key).</summary>
public class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<IReadOnlyList<VariantLookupItem>> LookupAsync(IReadOnlyList<Guid> variantIds, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("internal/variants/lookup",
                new VariantLookupRequest(variantIds.ToList()), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "Catalog service is unavailable. Please try again.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new AppException(StatusCodes.Status502BadGateway, "Catalog service returned an unexpected error.");

            return await response.Content.ReadFromJsonAsync<List<VariantLookupItem>>(ct) ?? new List<VariantLookupItem>();
        }
    }
}
