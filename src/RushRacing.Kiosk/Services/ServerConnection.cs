using Microsoft.AspNetCore.SignalR.Client;
using RushRacing.Kiosk.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO;

namespace RushRacing.Kiosk.Services;

public class ServerConnection : IAsyncDisposable
{
    private readonly KioskConfig _config;
    private HubConnection? _hubConnection;
    private readonly HttpClient _httpClient;
    private bool _isAuthenticated = false;
    private int _machineId;

    public event Action<SessionInfo>? OnStartSession;
    public event Action<SessionInfo>? OnResumeSession;
    public event Action<SessionInfo>? OnSessionConfirmed;
    public event Action<string>? OnAuthenticationFailed;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public bool IsAuthenticated => _isAuthenticated;

    public ServerConnection(KioskConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_config.ServerUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Machine-Code", _config.MachineCode);
        _httpClient.DefaultRequestHeaders.Add("X-Machine-Key", _config.ApiKey);
    }

    public async Task ConnectAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_config.HubUrl)
            .WithAutomaticReconnect(new[] {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60)
            })
            .Build();

        // Register handlers
        _hubConnection.On<int, string>("Authenticated", (machineId, machineCode) =>
        {
            _machineId = machineId;
            _isAuthenticated = true;
            System.Diagnostics.Debug.WriteLine($"[SERVER] Authenticated as {machineCode} (ID: {machineId})");
        });

        _hubConnection.On<string>("AuthenticationFailed", (message) =>
        {
            _isAuthenticated = false;
            OnAuthenticationFailed?.Invoke(message);
        });

        _hubConnection.On("StartSession", (SessionInfo session) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SERVER] StartSession received: {session.SessionCode}");
            OnStartSession?.Invoke(session);
        });

        _hubConnection.On("ResumeSession", (SessionInfo session) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SERVER] ResumeSession received: {session.SessionCode}");
            OnResumeSession?.Invoke(session);
        });

        _hubConnection.On("SessionConfirmed", (SessionInfo session) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SERVER] SessionConfirmed: expires {session.SessionExpiresAt}");
            OnSessionConfirmed?.Invoke(session);
        });

        _hubConnection.Reconnecting += (ex) =>
        {
            System.Diagnostics.Debug.WriteLine("[SERVER] Reconnecting...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async (connectionId) =>
        {
            System.Diagnostics.Debug.WriteLine("[SERVER] Reconnected. Re-registering...");
            await RegisterMachine();
            OnConnected?.Invoke();
        };

        _hubConnection.Closed += (ex) =>
        {
            System.Diagnostics.Debug.WriteLine("[SERVER] Connection closed.");
            _isAuthenticated = false;
            OnDisconnected?.Invoke();
            return Task.CompletedTask;
        };

        // Connect
        try
        {
            await _hubConnection.StartAsync();
            System.Diagnostics.Debug.WriteLine("[SERVER] Connected to hub.");
            await RegisterMachine();
            OnConnected?.Invoke();
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "kiosk_debug.log");
            File.AppendAllText(logPath,
                $"{DateTime.Now:HH:mm:ss} | CONNECTION ERROR: {ex.Message}\n{ex.StackTrace}\n\n");
            OnDisconnected?.Invoke();
        }
    }

    private async Task RegisterMachine()
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;

        await _hubConnection.InvokeAsync("RegisterMachine",
            _config.MachineCode, _config.ApiKey);
    }

    public async Task SendMachineReady()
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;
        await _hubConnection.InvokeAsync("MachineReady", _config.MachineCode);
    }

    public async Task SendGameSelected(string sessionCode, string gameId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;
        await _hubConnection.InvokeAsync("GameSelected", sessionCode, gameId);
    }

    public async Task SendSessionAcknowledged(string sessionCode)
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;
        await _hubConnection.InvokeAsync("SessionAcknowledged", sessionCode);
    }

    public async Task SendSessionCompleted(string sessionCode)
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;
        await _hubConnection.InvokeAsync("SessionCompleted", sessionCode);
    }

    public async Task SendMachineReset()
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;
        await _hubConnection.InvokeAsync("MachineReset", _config.MachineCode);
    }

    public async Task SendError(string sessionCode, string errorMessage)
    {
        if (_hubConnection?.State != HubConnectionState.Connected) return;
        await _hubConnection.InvokeAsync("ReportError",
            _config.MachineCode, sessionCode, errorMessage);
    }

    public async Task SendHeartbeat(HeartbeatData data)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/machine/heartbeat", data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HEARTBEAT] Failed: {ex.Message}");
        }
    }

    public async Task<SessionInfo?> GetActiveSessionFromServer()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/machine/session/{_config.MachineCode}");

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content
                .ReadFromJsonAsync<ActiveSessionResponse>();

            if (result == null || !result.HasActiveSession) return null;

            return result.Session;
        }
        catch
        {
            return null;
        }
    }

    public async Task StartReconnectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_hubConnection?.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await _hubConnection.StartAsync(ct);
                    await RegisterMachine();
                    OnConnected?.Invoke();
                }
                catch
                {
                    // Will retry
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        _httpClient.Dispose();
    }
}

public class HeartbeatData
{
    public double? CpuPercent { get; set; }
    public double? RamPercent { get; set; }
    public double? GpuTempCelsius { get; set; }
    public double? DiskFreeGb { get; set; }
    public string? InternetStatus { get; set; }
    public string? AgentVersion { get; set; }
    public bool? GameProcessRunning { get; set; }
    public string? ActiveSessionCode { get; set; }
    public string? ActiveGameId { get; set; }
}

public class ActiveSessionResponse
{
    public bool HasActiveSession { get; set; }
    public SessionInfo? Session { get; set; }
}