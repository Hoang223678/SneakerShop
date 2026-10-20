using Microsoft.AspNetCore.Mvc;
using SneakerShop.Contracts;

namespace Catalog.Api.Interfaces;

public interface IInternalVariantsController
{
    Task<ActionResult<List<VariantLookupItem>>> Lookup(VariantLookupRequest request, CancellationToken ct);
}
