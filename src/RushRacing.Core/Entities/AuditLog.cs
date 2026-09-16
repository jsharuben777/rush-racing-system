namespace RushRacing.Core.Entities;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public int? AdminUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public AdminUser? AdminUser { get; set; }
}