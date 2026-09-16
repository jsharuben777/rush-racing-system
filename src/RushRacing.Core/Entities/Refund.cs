using RushRacing.Core.Enums;

namespace RushRacing.Core.Entities;

public class Refund
{
    public int RefundId { get; set; }
    public int PaymentId { get; set; }
    public int SessionId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";              // Pending, Processed, Failed
    public string InitiatedBy { get; set; } = string.Empty;      // "SYSTEM", "ADMIN:jay"
    public string? ExternalRefundId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Payment Payment { get; set; } = null!;
    public Session Session { get; set; } = null!;
}