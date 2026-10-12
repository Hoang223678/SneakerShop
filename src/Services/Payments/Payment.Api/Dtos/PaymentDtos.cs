using System.ComponentModel.DataAnnotations;
using Payment.Api.Domain;

namespace Payment.Api.Dtos;

/// <summary>Starts or retries payment for a confirmed order.</summary>
public sealed record CreatePaymentRequest
{
    [Required]
    public required Guid OrderId { get; init; }

    [Required, MaxLength(30)]
    public required string Method { get; init; }
}

/// <summary>Requests a full or partial refund. The caller supplies a stable refund idempotency key.</summary>
public sealed record CreateRefundRequest
{
    [Range(0.01, 999999999)]
    public required decimal Amount { get; init; }
}

/// <summary>Payment information safe to return to the owning customer.</summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTimeOffset ExpiresAt,
    Guid? AttemptId,
    string? PaymentUrl);
