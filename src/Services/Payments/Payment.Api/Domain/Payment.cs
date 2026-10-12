namespace Payment.Api.Domain;

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Expired,
    Refunded
}

public sealed class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal AmountRefunded { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PaymentAttempt> Attempts { get; set; } = [];
    public List<PaymentRefund> Refunds { get; set; } = [];
}

public sealed class PaymentRefund
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public bool Completed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public PaymentRecord Payment { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string? PaymentUrl { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
