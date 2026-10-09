using System.ComponentModel.DataAnnotations;
using Catalog.Api.Domain;

namespace Catalog.Api.Dtos;

public record BrandDto(int Id, string Name, string Slug);

public record SneakerListItem(Guid Id, string Name, string Slug, string Brand, Gender Gender, decimal Price, string? ImageUrl);

public record VariantDto(Guid Id, string Sku, decimal Size, string Color);

public record SneakerDetail(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    BrandDto Brand,
    Gender Gender,
    decimal Price,
    string? ImageUrl,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<VariantDto> Variants);

public class SneakerQuery
{
    public string? Search { get; set; }
    public int? BrandId { get; set; }
    public Gender? Gender { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    /// <summary>name (default) | price_asc | price_desc | newest</summary>
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public class CreateBrandRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class SneakerUpsertRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public int BrandId { get; set; }

    [Required]
    public Gender Gender { get; set; } = Gender.Unisex;

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }
}

public class CreateVariantRequest
{
    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Range(1, 25)]
    public decimal Size { get; set; }

    [Required, MaxLength(50)]
    public string Color { get; set; } = string.Empty;
}
