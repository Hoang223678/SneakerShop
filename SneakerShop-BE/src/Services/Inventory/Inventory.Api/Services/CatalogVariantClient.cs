using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SneakerShop.BuildingBlocks;

namespace Inventory.Api.Services;

public sealed record CatalogVariantDto(Guid Id, Guid SneakerId, string Sku, decimal Size, string Color);

public interface ICatalogVariantClient
{
    Task<CatalogVariantDto> CreateAsync(Guid sneakerId, string sku, decimal size, string color, CancellationToken ct);
    Task<CatalogVariantDto> UpdateAsync(Guid variantId, string sku, decimal size, string color, CancellationToken ct);
    Task DeleteAsync(Guid variantId, CancellationToken ct);
}

public sealed class CatalogVariantClient(HttpClient http) : ICatalogVariantClient
{
    public Task<CatalogVariantDto> CreateAsync(Guid sneakerId, string sku, decimal size, string color, CancellationToken ct) =>
        SendAsync<CatalogVariantDto>(HttpMethod.Post, $"api/sneakers/{sneakerId}/variants", new { sku, size, color }, ct);

    public Task<CatalogVariantDto> UpdateAsync(Guid variantId, string sku, decimal size, string color, CancellationToken ct) =>
        SendAsync<CatalogVariantDto>(HttpMethod.Put, $"api/variants/{variantId}", new { sku, size, color }, ct);

    public async Task DeleteAsync(Guid variantId, CancellationToken ct)
    {
        await SendAsync<object>(HttpMethod.Delete, $"api/variants/{variantId}", body: null, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null)
            message.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "Catalog service is unavailable. Please try again.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
                return default!;
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>(ct)
                       ?? throw new AppException(StatusCodes.Status502BadGateway, "Catalog service returned an unexpected error.");

            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
                if (!string.IsNullOrWhiteSpace(problem?.Title))
                    throw new AppException((int)response.StatusCode, problem.Title);
            }
            catch (JsonException)
            {
            }
            catch (AppException)
            {
                throw;
            }

            throw new AppException((int)response.StatusCode, "Catalog service returned an unexpected error.");
        }
    }
}
