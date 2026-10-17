using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Payment.Api.Dtos;
using Payment.Api.Providers;
using Payment.Api.Services;
using SneakerShop.BuildingBlocks;

namespace Payment.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(IPaymentService payments, IOptions<VnPayOptions> vnPay) : ControllerBase
{
    /// <summary>Create a payment for a confirmed order. Method <c>vnpay</c> returns a VNPay sandbox checkout URL.</summary>
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

    /// <summary>VNPay server-to-server IPN. Confirms the signed payment result.</summary>
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

    /// <summary>VNPay sandbox browser return. Verifies the signed query then redirects to the frontend.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("payment-webhook")]
    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct)
    {
        var payload = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());
        var succeeded = false;
        try
        {
            await payments.ProcessWebhookAsync("VnPay", payload, ct);
            succeeded = payload.GetValueOrDefault("vnp_ResponseCode") == "00"
                && payload.GetValueOrDefault("vnp_TransactionStatus") == "00";
        }
        catch (AppException)
        {
            succeeded = false;
        }

        var frontend = vnPay.Value.FrontendResultUrl;
        var status = succeeded ? "success" : "failed";
        var attemptId = payload.GetValueOrDefault("vnp_TxnRef");
        if (!string.IsNullOrWhiteSpace(frontend))
            return Redirect(QueryHelpers.AddQueryString(frontend, new Dictionary<string, string?>
            {
                ["status"] = status,
                ["attemptId"] = attemptId
            }));

        return Content(
            $"""
            <!doctype html>
            <html lang="vi"><head><meta charset="utf-8"><title>Kết quả thanh toán</title></head>
            <body style="font-family:sans-serif;max-width:40rem;margin:4rem auto">
            <h1>{(succeeded ? "Thanh toán thành công" : "Thanh toán chưa được xác nhận")}</h1>
            <p>Mã giao dịch: {System.Net.WebUtility.HtmlEncode(attemptId)}</p>
            </body></html>
            """,
            "text/html");
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
