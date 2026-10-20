using Cart.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Cart.Api.Interfaces;

public interface IInternalCartController
{
    Task<ActionResult<InternalCartDto>> Get(Guid userId, CancellationToken ct);
}
