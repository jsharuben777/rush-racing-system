using System.Diagnostics;
using System.IO;

namespace RushRacing.Kiosk.Services;

public class HeartbeatService
{
    private readonly ServerConnection _server;
    private readonly int _intervalSeconds;
    private string? _activeSessionCode;
    private string? _activeGameId;
    private bool _gameRunning;

    public HeartbeatService(ServerConnection server, int intervalSeconds)
    {
        _server = server;
        _intervalSeconds = intervalSeconds;
    }

    public void UpdateSessionInfo(string? sessionCode, string? gameId, bool gameRunning)
    {
        _activeSessionCode = sessionCode;
        _activeGameId = gameId;
        _gameRunning = gameRunning;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        System.Diagnostics.Debug.WriteLine("[HEARTBEAT] Service started.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var data = new HeartbeatData
                {
                    CpuPercent = GetCpuUsage(),
                    RamPercent = GetRamUsage(),
                    DiskFreeGb = GetDiskFreeGb(),
                    InternetStatus = _server.IsConnected ? "OK" : "DOWN",
                    AgentVersion = "1.0.0",
                    GameProcessRunning = _gameRunning,
                    ActiveSessionCode = _activeSessionCode,
                    ActiveGameId = _activeGameId
                };

                await _server.SendHeartbeat(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HEARTBEAT] Error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), ct);
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            // Simple approximation
            return 0; // TODO: implement with PerformanceCounter if needed
        }
        catch { return 0; }
    }

    private double GetRamUsage()
    {
        try
        {
            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var usedMemory = totalMemory - GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return 0; // TODO: implement properly
        }
        catch { return 0; }
    }

    private double GetDiskFreeGb()
    {
        try
        {
            var drive = new DriveInfo("C");
            return Math.Round(drive.AvailableFreeSpace / 1_073_741_824.0, 1);
        }
        catch { return 0; }
    }
}