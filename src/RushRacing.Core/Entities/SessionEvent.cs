namespace RushRacing.Core.Entities;

public class SessionEvent
{
    public long EventId { get; set; }
    public int? SessionId { get; set; }
    public int? MachineId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Source { get; set; } = string.Empty;           // "SERVER", "AGENT", "ADMIN", "WEBHOOK"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Session? Session { get; set; }
    public Machine? Machine { get; set; }
}