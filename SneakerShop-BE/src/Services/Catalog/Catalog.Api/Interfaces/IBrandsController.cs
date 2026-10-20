using Catalog.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Interfaces;

public interface IBrandsController
{
    Task<ActionResult<List<BrandDto>>> List(CancellationToken ct);
    Task<ActionResult<BrandDto>> Create(CreateBrandRequest request, CancellationToken ct);
    Task<ActionResult<BrandDto>> Update(int id, CreateBrandRequest request, CancellationToken ct);
    Task<IActionResult> Delete(int id, CancellationToken ct);
}
