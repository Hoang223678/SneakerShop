using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Controllers;

namespace Ordering.Api.Interfaces;

public interface IInternalOrdersController
{
    Task<ActionResult<OrderPaymentDetails>> GetPaymentDetails(Guid id, CancellationToken ct);
}
