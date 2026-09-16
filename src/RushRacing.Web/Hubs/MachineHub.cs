using Microsoft.AspNetCore.SignalR;
using RushRacing.Core.Enums;
using RushRacing.Core.Interfaces;
using RushRacing.Data;
using Microsoft.EntityFrameworkCore;

namespace RushRacing.Web.Hubs;

public class MachineHub : Hub
{
    private readonly IMachineService _machineService;
    private readonly ISessionService _sessionService;
    private readonly ISessionEventService _events;
    private readonly RushRacingDbContext _db;

    // Store connection-to-machine mapping
    private static readonly Dictionary<string, int> ConnectionMachineMap = new();
    private static readonly Dictionary<int, string> MachineConnectionMap = new();
    private static readonly object _lock = new();

    public MachineHub(
        IMachineService machineService,
        ISessionService sessionService,
        ISessionEventService events,
        RushRacingDbContext db)
    {
        _machineService = machineService;
        _sessionService = sessionService;
        _events = events;
        _db = db;
    }

    /// <summary>
    /// Called by the Agent when it connects and registers itself.
    /// </summary>
    public async Task RegisterMachine(string machineCode, string apiKey)
    {
        // Authenticate
        var isAuthentic = await _machineService.AuthenticateMachine(machineCode, apiKey);
        if (!isAuthentic)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid credentials.");
            Context.Abort();
            return;
        }

        var machine = await _machineService.GetByMachineCode(machineCode);
        if (machine == null)
        {
            Context.Abort();
            return;
        }

        // Register connection
        lock (_lock)
        {
            ConnectionMachineMap[Context.ConnectionId] = machine.MachineId;
            MachineConnectionMap[machine.MachineId] = Context.ConnectionId;
        }

        // Update machine status
        try
        {
            await _machineService.UpdateStatus(machine.MachineId, MachineStatus.Booting);
        }
        catch (InvalidOperationException)
        {
            // State transition may not be valid from current state — force it
            machine.Status = MachineStatus.Booting;
            await _db.SaveChangesAsync();
        }

        await _events.Log(null, machine.MachineId,
            "MACHINE_CONNECTED",
            $"Machine {machineCode} connected. ConnectionId: {Context.ConnectionId}",
            "AGENT");

        // Tell the agent it is authenticated
        await Clients.Caller.SendAsync("Authenticated", machine.MachineId, machine.MachineCode);

        // Check if there is an active session to resume
        var activeSession = await _sessionService.GetActiveSessionForMachine(machine.MachineId);
        if (activeSession != null)
        {
            await Clients.Caller.SendAsync("ResumeSession", new
            {
                activeSession.SessionCode,
                activeSession.SelectedGameId,
                activeSession.SessionExpiresAt,
                activeSession.Status
            });
        }
    }

    /// <summary>
    /// Called by the Agent when it has finished booting and is ready.
    /// </summary>
    public async Task MachineReady(string machineCode)
    {
        var machine = await _machineService.GetByMachineCode(machineCode);
        if (machine == null) return;

        await _machineService.UpdateStatus(machine.MachineId, MachineStatus.Ready);

        await _events.Log(null, machine.MachineId,
            "MACHINE_READY",
            $"Machine {machineCode} is ready",
            "AGENT");
    }

    /// <summary>
    /// Called by the Agent when the customer selects a game.
    /// </summary>
    public async Task GameSelected(string sessionCode, string gameId)
    {
        await _sessionService.MarkGameSelected(sessionCode, gameId);
    }

    /// <summary>
    /// Called by the Agent when the game is running and session should start timing.
    /// </summary>
    public async Task SessionAcknowledged(string sessionCode)
    {
        var session = await _sessionService.GetBySessionCode(sessionCode);
        if (session == null) return;

        await _sessionService.MarkSessionStarted(sessionCode);

        // Reload to get the computed expiry
        session = await _sessionService.GetBySessionCode(sessionCode);

        // Tell the Agent the authoritative expiry time
        await Clients.Caller.SendAsync("SessionConfirmed", new
        {
            session!.SessionCode,
            session.SessionExpiresAt
        });

        // Update machine status
        await _machineService.UpdateStatus(session.MachineId, MachineStatus.InSession);
    }

    /// <summary>
    /// Called by the Agent when the session timer expires and the machine is resetting.
    /// </summary>
    public async Task SessionCompleted(string sessionCode)
    {
        await _sessionService.MarkSessionCompleted(sessionCode);
    }

    /// <summary>
    /// Called by the Agent when the machine has finished resetting and is ready again.
    /// </summary>
    public async Task MachineReset(string machineCode)
    {
        var machine = await _machineService.GetByMachineCode(machineCode);
        if (machine == null) return;

        await _machineService.UpdateStatus(machine.MachineId, MachineStatus.Ready);
    }

    /// <summary>
    /// Called by the Agent to report an error.
    /// </summary>
    public async Task ReportError(string machineCode, string sessionCode, string errorMessage)
    {
        var machine = await _machineService.GetByMachineCode(machineCode);
        if (machine == null) return;

        await _machineService.UpdateStatus(machine.MachineId, MachineStatus.Error);

        if (!string.IsNullOrEmpty(sessionCode))
        {
            await _sessionService.MarkSessionError(sessionCode, errorMessage);
        }

        await _events.Log(null, machine.MachineId,
            "MACHINE_ERROR",
            errorMessage,
            "AGENT");

        // TODO: Send alert to admins via Telegram
    }

    /// <summary>
    /// Static method to send a StartSession command to a specific machine.
    /// Called by the payment handler.
    /// </summary>
    public static async Task SendStartSession(
        IHubContext<MachineHub> hubContext,
        int machineId,
        string sessionCode,
        int durationMinutes)
    {
        string? connectionId;
        lock (_lock)
        {
            MachineConnectionMap.TryGetValue(machineId, out connectionId);
        }

        if (connectionId != null)
        {
            await hubContext.Clients.Client(connectionId)
                .SendAsync("StartSession", new
                {
                    SessionCode = sessionCode,
                    DurationMinutes = durationMinutes
                });
        }
    }

    /// <summary>
    /// Static method to check if a machine is connected.
    /// </summary>
    public static bool IsMachineConnected(int machineId)
    {
        lock (_lock)
        {
            return MachineConnectionMap.ContainsKey(machineId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        int machineId;
        lock (_lock)
        {
            if (ConnectionMachineMap.TryGetValue(Context.ConnectionId, out machineId))
            {
                ConnectionMachineMap.Remove(Context.ConnectionId);
                MachineConnectionMap.Remove(machineId);
            }
            else
            {
                return;
            }
        }

        // Mark machine as offline
        var machine = await _db.Machines.FindAsync(machineId);
        if (machine != null)
        {
            machine.Status = MachineStatus.Offline;
            await _db.SaveChangesAsync();
        }

        await _events.Log(null, machineId,
            "MACHINE_DISCONNECTED",
            $"Machine disconnected. ConnectionId: {Context.ConnectionId}",
            "SERVER");

        await base.OnDisconnectedAsync(exception);
    }
}