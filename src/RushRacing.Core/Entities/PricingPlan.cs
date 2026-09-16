namespace RushRacing.Core.Entities;

public class PricingPlan
{
    public int PricingPlanId { get; set; }
    public int? LocationId { get; set; }                          // null = global
    public int DurationMinutes { get; set; }
    public decimal PriceAmount { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Location? Location { get; set; }
}