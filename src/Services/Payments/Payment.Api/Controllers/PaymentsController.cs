using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Payment.Api.Dtos;
using Payment.Api.Services;
using SneakerShop.BuildingBlocks;

namespace Payment.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(IPaymentService payments) : ControllerBase
{
    /// <summary>Create a payment or a new attempt for a confirmed order.</summary>
    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest request, CancellationToken ct)
    {
        var response = await payments.CreateAsync(
            request.OrderId,
            User.GetUserId(),
            request.Method,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            ct);
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    /// <summary>Get the current payment status for this order.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> Get(Guid id, CancellationToken ct) =>
        await payments.GetAsync(id, User.GetUserId(), User.IsAdmin(), ct);

    /// <summary>Request a full or partial refund (admin only).</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:guid}/refund")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Refund(
        Guid id,
        CreateRefundRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!Guid.TryParse(idempotencyKey, out var refundId) || refundId == Guid.Empty)
            return BadRequest(new ProblemDetails
            {
                Title = "A valid GUID Idempotency-Key header is required.",
                Status = StatusCodes.Status400BadRequest
            });
        var response = await payments.RequestRefundAsync(id, refundId, request.Amount, ct);
        return AcceptedAtAction(nameof(Get), new { id }, response);
    }

    /// <summary>VNPay IPN endpoint. A browser return URL never changes payment state.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("payment-webhook")]
    [HttpGet("webhooks/vnpay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VnPayWebhook(CancellationToken ct)
    {
        var payload = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());
        await payments.ProcessWebhookAsync("VnPay", payload, ct);
        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }

    /// <summary>Development-only helper for completing a fake payment.</summary>
    [HttpPost("dev/attempts/{attemptId:guid}/succeed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SimulateSuccess(Guid attemptId, CancellationToken ct)
    {
        await payments.SimulateSuccessAsync(attemptId, User.GetUserId(), ct);
        return NoContent();
    }
}
