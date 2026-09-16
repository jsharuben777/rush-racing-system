namespace RushRacing.Core.Entities;

public class Location
{
    public int LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string TimeZone { get; set; } = "Asia/Kuala_Lumpur";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public List<Machine> Machines { get; set; } = new();
    public List<PricingPlan> PricingPlans { get; set; } = new();
}