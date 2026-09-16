namespace RushRacing.Kiosk.Models;

public class SessionInfo
{
    public string SessionCode { get; set; } = string.Empty;
    public string? SelectedGameId { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset? SessionExpiresAt { get; set; }
    public string? Status { get; set; }
}