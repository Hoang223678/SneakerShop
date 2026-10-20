using Microsoft.AspNetCore.Mvc;
using Payment.Api.Dtos;

namespace Payment.Api.Interfaces;

public interface IPaymentsController
{
    Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest request, CancellationToken ct);
    Task<ActionResult<PaymentResponse>> Get(Guid id, CancellationToken ct);
    Task<ActionResult<PaymentResponse>> Refund(Guid id, CreateRefundRequest request, string? idempotencyKey, CancellationToken ct);
    Task<IActionResult> VnPayWebhook(CancellationToken ct);
    Task<IActionResult> VnPayReturn(CancellationToken ct);
    Task<IActionResult> SimulateSuccess(Guid attemptId, CancellationToken ct);
}
