namespace SneakerShop.Contracts;

/// <summary>Internal service-to-service contract: Ordering asks Catalog for authoritative variant data and prices.</summary>
public record VariantLookupRequest(List<Guid> VariantIds);

public record VariantLookupItem(
    Guid VariantId,
    Guid SneakerId,
    string SneakerName,
    string Sku,
    decimal Size,
    string Color,
    decimal UnitPrice);
