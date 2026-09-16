using RushRacing.Core.Enums;

namespace RushRacing.Core.Entities;

public class Session
{
    public int SessionId { get; set; }
    public string SessionCode { get; set; } = string.Empty;       // "RR82931"
    public int MachineId { get; set; }
    public int PricingPlanId { get; set; }
    public string? SelectedGameId { get; set; }
    public int DurationMinutes { get; set; }
    public decimal PriceAmount { get; set; }
    public string Currency { get; set; } = "MYR";
    public SessionStatus Status { get; set; } = SessionStatus.Created;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public DateTimeOffset? PaymentStartedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? GameSelectedAt { get; set; }
    public DateTimeOffset? SessionStartedAt { get; set; }
    public DateTimeOffset? SessionExpiresAt { get; set; }
    public DateTimeOffset? SessionEndedAt { get; set; }
    public DateTimeOffset? MachineAcknowledgedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Machine Machine { get; set; } = null!;
    public PricingPlan PricingPlan { get; set; } = null!;
    public Game? SelectedGame { get; set; }
    public List<Payment> Payments { get; set; } = new();
    public List<SessionEvent> Events { get; set; } = new();
}