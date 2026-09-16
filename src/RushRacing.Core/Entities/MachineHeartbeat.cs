namespace RushRacing.Core.Entities;

public class MachineHeartbeat
{
    public long HeartbeatId { get; set; }
    public int MachineId { get; set; }
    public double? CpuPercent { get; set; }
    public double? RamPercent { get; set; }
    public double? GpuTempCelsius { get; set; }
    public double? DiskFreeGb { get; set; }
    public string? InternetStatus { get; set; }                  // "OK", "SLOW", "DOWN"
    public string? AgentVersion { get; set; }
    public bool? GameProcessRunning { get; set; }
    public string? ActiveSessionCode { get; set; }
    public string? ActiveGameId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Machine Machine { get; set; } = null!;
}