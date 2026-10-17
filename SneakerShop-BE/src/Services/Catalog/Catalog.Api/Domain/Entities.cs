namespace Catalog.Api.Domain;

public enum Gender
{
    Men,
    Women,
    Unisex,
    Kids
}

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ICollection<Sneaker> Sneakers { get; set; } = new List<Sneaker>();
}

public class Sneaker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public Gender Gender { get; set; } = Gender.Unisex;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<SneakerVariant> Variants { get; set; } = new List<SneakerVariant>();
}

/// <summary>A purchasable size/color of a sneaker. This is the unit that Inventory and Ordering refer to.</summary>
public class SneakerVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SneakerId { get; set; }
    public Sneaker Sneaker { get; set; } = null!;
    public string Sku { get; set; } = string.Empty;
    public decimal Size { get; set; }          // US size, e.g. 9.5
    public string Color { get; set; } = string.Empty;
}
