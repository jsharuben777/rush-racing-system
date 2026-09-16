using RushRacing.Core.Enums;

namespace RushRacing.Core.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    public int SessionId { get; set; }
    public string? ExternalPaymentId { get; set; }
    public string PaymentProvider { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MYR";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? RawWebhookPayload { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Session Session { get; set; } = null!;
    public List<Refund> Refunds { get; set; } = new();
}