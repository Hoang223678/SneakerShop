using Catalog.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using SneakerShop.BuildingBlocks;

namespace Catalog.Api.Interfaces;

public interface ISneakersController
{
    Task<ActionResult<PagedResult<SneakerListItem>>> List(SneakerQuery q, CancellationToken ct);
    Task<ActionResult<List<SneakerListItem>>> BestSellers(int days = 7, int take = 10, CancellationToken ct = default);
    Task<ActionResult<SneakerDetail>> Get(Guid id, CancellationToken ct);
    Task<ActionResult<SneakerDetail>> Create(SneakerUpsertRequest request, CancellationToken ct);
    Task<ActionResult<SneakerDetail>> Update(Guid id, SneakerUpsertRequest request, CancellationToken ct);
    Task<IActionResult> Delete(Guid id, CancellationToken ct);
    Task<ActionResult<List<SneakerColorVariantsDto>>> CreateByColor(CreateColorVariantsRequest request, CancellationToken ct);
    Task<ActionResult<VariantDto>> AddVariant(Guid id, CreateVariantRequest request, CancellationToken ct);
}
