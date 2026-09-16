namespace RushRacing.Kiosk.Models;

public class GameDefinition
{
    public string GameId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ImageFileName { get; set; } = string.Empty;
    public string LaunchMethod { get; set; } = "steam";
    public string SteamAppId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int LaunchTimeoutSeconds { get; set; } = 90;
    public string[] ConfigResetPaths { get; set; } = Array.Empty<string>();
}