namespace RushRacing.Kiosk.Models;

public class KioskConfig
{
    public string ServerUrl { get; set; } = string.Empty;
    public string HubUrl { get; set; } = string.Empty;
    public string PublicBookingUrl { get; set; } = string.Empty;
    public string MachineCode { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int GameSelectTimeoutSeconds { get; set; } = 90;
    public int GameSelectFinalCountdownSeconds { get; set; } = 30;
    public int MaxGameCrashesBeforeError { get; set; } = 3;
    public int CrashWindowMinutes { get; set; } = 10;
}
