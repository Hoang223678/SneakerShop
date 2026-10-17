namespace SneakerShop.BuildingBlocks;

/// <summary>
/// Demo seed data shared by Catalog (products) and Inventory (stock) so both services start with matching variant IDs.
/// Generated once; edit freely. Real data should come through the Catalog API + events.
/// </summary>
public record SeedSneaker(Guid Id, string Code, string Name, string Brand, string Gender, decimal Price, string Color, string Description, string ImageUrl);
public record SeedVariant(Guid Id, Guid SneakerId, string Sku, decimal Size, string Color);

public static class SeedCatalog
{
    public const int InitialStockPerVariant = 20;

    public static readonly IReadOnlyList<SeedSneaker> Sneakers = new List<SeedSneaker>
    {
        new(Guid.Parse("00000001-0000-0000-0000-000000000001"), "NK-AM90", "Nike Air Max 90", "Nike", "Men", 129.99m, "White/Black",
            "Retro runner silhouette with a visible Air unit in the heel and a durable leather and mesh upper.",
            "https://placehold.co/600x400?text=Nike+Air+Max+90"),
        new(Guid.Parse("00000001-0000-0000-0000-000000000002"), "NK-DNK", "Nike Dunk Low", "Nike", "Unisex", 119.99m, "Panda",
            "Court-inspired low-top with a clean two-tone leather upper and cupsole construction.",
            "https://placehold.co/600x400?text=Nike+Dunk+Low"),
        new(Guid.Parse("00000001-0000-0000-0000-000000000003"), "AD-SMB", "Adidas Samba OG", "Adidas", "Unisex", 109.99m, "Core Black",
            "Terrace-style trainer with a soft leather upper, suede overlays and a gum rubber outsole.",
            "https://placehold.co/600x400?text=Adidas+Samba+OG"),
        new(Guid.Parse("00000001-0000-0000-0000-000000000004"), "AD-UBL", "Adidas Ultraboost Light", "Adidas", "Men", 189.99m, "Cloud White",
            "Lightweight running shoe with a responsive foam midsole and a sock-like knit upper.",
            "https://placehold.co/600x400?text=Adidas+Ultraboost+Light"),
        new(Guid.Parse("00000001-0000-0000-0000-000000000005"), "NB-550", "New Balance 550", "New Balance", "Unisex", 129.99m, "White/Green",
            "Basketball-heritage low-top with a leather upper and a cushioned, flat-profile sole.",
            "https://placehold.co/600x400?text=New+Balance+550"),
        new(Guid.Parse("00000001-0000-0000-0000-000000000006"), "PM-SUE", "Puma Suede Classic", "Puma", "Unisex", 79.99m, "Peacoat",
            "Iconic suede sneaker with a formstrip, padded collar and rubber outsole.",
            "https://placehold.co/600x400?text=Puma+Suede+Classic"),
    };

    public static readonly IReadOnlyList<SeedVariant> Variants = new List<SeedVariant>
    {
        new(Guid.Parse("00000002-0000-0000-0000-000000000001"), Guid.Parse("00000001-0000-0000-0000-000000000001"), "NK-AM90-WHITE-BLACK-8.0", 8m, "White/Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000002"), Guid.Parse("00000001-0000-0000-0000-000000000001"), "NK-AM90-WHITE-BLACK-9.0", 9m, "White/Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000003"), Guid.Parse("00000001-0000-0000-0000-000000000001"), "NK-AM90-WHITE-BLACK-10.0", 10m, "White/Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000004"), Guid.Parse("00000001-0000-0000-0000-000000000001"), "NK-AM90-WHITE-BLACK-11.0", 11m, "White/Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000005"), Guid.Parse("00000001-0000-0000-0000-000000000002"), "NK-DNK-PANDA-8.0", 8m, "Panda"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000006"), Guid.Parse("00000001-0000-0000-0000-000000000002"), "NK-DNK-PANDA-9.0", 9m, "Panda"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000007"), Guid.Parse("00000001-0000-0000-0000-000000000002"), "NK-DNK-PANDA-10.0", 10m, "Panda"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000008"), Guid.Parse("00000001-0000-0000-0000-000000000002"), "NK-DNK-PANDA-11.0", 11m, "Panda"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000009"), Guid.Parse("00000001-0000-0000-0000-000000000003"), "AD-SMB-COREBLACK-8.0", 8m, "Core Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000010"), Guid.Parse("00000001-0000-0000-0000-000000000003"), "AD-SMB-COREBLACK-9.0", 9m, "Core Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000011"), Guid.Parse("00000001-0000-0000-0000-000000000003"), "AD-SMB-COREBLACK-10.0", 10m, "Core Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000012"), Guid.Parse("00000001-0000-0000-0000-000000000003"), "AD-SMB-COREBLACK-11.0", 11m, "Core Black"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000013"), Guid.Parse("00000001-0000-0000-0000-000000000004"), "AD-UBL-CLOUDWHITE-8.0", 8m, "Cloud White"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000014"), Guid.Parse("00000001-0000-0000-0000-000000000004"), "AD-UBL-CLOUDWHITE-9.0", 9m, "Cloud White"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000015"), Guid.Parse("00000001-0000-0000-0000-000000000004"), "AD-UBL-CLOUDWHITE-10.0", 10m, "Cloud White"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000016"), Guid.Parse("00000001-0000-0000-0000-000000000004"), "AD-UBL-CLOUDWHITE-11.0", 11m, "Cloud White"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000017"), Guid.Parse("00000001-0000-0000-0000-000000000005"), "NB-550-WHITE-GREEN-8.0", 8m, "White/Green"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000018"), Guid.Parse("00000001-0000-0000-0000-000000000005"), "NB-550-WHITE-GREEN-9.0", 9m, "White/Green"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000019"), Guid.Parse("00000001-0000-0000-0000-000000000005"), "NB-550-WHITE-GREEN-10.0", 10m, "White/Green"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000020"), Guid.Parse("00000001-0000-0000-0000-000000000005"), "NB-550-WHITE-GREEN-11.0", 11m, "White/Green"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000021"), Guid.Parse("00000001-0000-0000-0000-000000000006"), "PM-SUE-PEACOAT-8.0", 8m, "Peacoat"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000022"), Guid.Parse("00000001-0000-0000-0000-000000000006"), "PM-SUE-PEACOAT-9.0", 9m, "Peacoat"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000023"), Guid.Parse("00000001-0000-0000-0000-000000000006"), "PM-SUE-PEACOAT-10.0", 10m, "Peacoat"),
        new(Guid.Parse("00000002-0000-0000-0000-000000000024"), Guid.Parse("00000001-0000-0000-0000-000000000006"), "PM-SUE-PEACOAT-11.0", 11m, "Peacoat"),
    };
}
