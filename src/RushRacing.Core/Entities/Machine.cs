using RushRacing.Core.Enums;

namespace RushRacing.Core.Entities;

public class Machine
{
    public int MachineId { get; set; }
    public int LocationId { get; set; }
    public string MachineCode { get; set; } = string.Empty;       // "RR-01"
    public string DisplayName { get; set; } = string.Empty;       // "Cockpit 01"
    public string Slug { get; set; } = string.Empty;              // "ck01" (used in QR URL)
    public string ApiKeyHash { get; set; } = string.Empty;
    public MachineStatus Status { get; set; } = MachineStatus.Offline;
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Location Location { get; set; } = null!;
    public List<Session> Sessions { get; set; } = new();
    public List<MachineHeartbeat> Heartbeats { get; set; } = new();
}