using Catalog.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Interfaces;

public interface IVariantsController
{
    Task<ActionResult<List<VariantListItem>>> List(Guid? sneakerId, CancellationToken ct);
    Task<ActionResult<VariantDto>> Create(Guid sneakerId, CreateVariantRequest request, CancellationToken ct);
    Task<ActionResult<VariantDto>> Update(Guid id, CreateVariantRequest request, CancellationToken ct);
    Task<IActionResult> Delete(Guid id, CancellationToken ct);
}
