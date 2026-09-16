using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using RushRacing.Core.Helpers;
using RushRacing.Core.Interfaces;
using RushRacing.Core.StateMachines;

namespace RushRacing.Data.Services;

public class MachineService : IMachineService
{
    private readonly RushRacingDbContext _db;
    private readonly ISessionEventService _events;

    public MachineService(RushRacingDbContext db, ISessionEventService events)
    {
        _db = db;
        _events = events;
    }

    public async Task<Machine?> GetBySlug(string slug)
    {
        return await _db.Machines
            .Include(m => m.Location)
            .FirstOrDefaultAsync(m => m.Slug == slug);
    }

    public async Task<Machine?> GetByMachineCode(string machineCode)
    {
        return await _db.Machines
            .Include(m => m.Location)
            .FirstOrDefaultAsync(m => m.MachineCode == machineCode);
    }

    public async Task<Machine?> GetById(int machineId)
    {
        return await _db.Machines
            .Include(m => m.Location)
            .FirstOrDefaultAsync(m => m.MachineId == machineId);
    }

    public async Task<List<Machine>> GetAllWithStatus()
    {
        return await _db.Machines
            .Include(m => m.Location)
            .OrderBy(m => m.LocationId)
            .ThenBy(m => m.MachineCode)
            .ToListAsync();
    }

    public async Task<bool> UpdateStatus(int machineId, MachineStatus newStatus)
    {
        var machine = await _db.Machines.FindAsync(machineId);
        if (machine == null) return false;

        MachineStateMachine.ValidateTransition(machine.Status, newStatus);

        machine.Status = newStatus;
        await _db.SaveChangesAsync();

        await _events.Log(null, machineId,
            "MACHINE_STATUS_CHANGED",
            $"Machine status changed to {newStatus}",
            "SERVER");

        return true;
    }

    public async Task RecordHeartbeat(int machineId, MachineHeartbeat heartbeat)
    {
        heartbeat.MachineId = machineId;
        heartbeat.ReceivedAt = DateTimeOffset.UtcNow;

        _db.MachineHeartbeats.Add(heartbeat);

        // Update machine's last heartbeat time
        var machine = await _db.Machines.FindAsync(machineId);
        if (machine != null)
        {
            machine.LastHeartbeatAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> IsMachineAvailable(int machineId)
    {
        var machine = await _db.Machines.FindAsync(machineId);
        if (machine == null) return false;

        return machine.IsEnabled && machine.Status == MachineStatus.Ready;
    }

    public async Task<bool> AuthenticateMachine(string machineCode, string apiKey)
    {
        var machine = await _db.Machines
            .FirstOrDefaultAsync(m => m.MachineCode == machineCode);

        if (machine == null) return false;
        if (!machine.IsEnabled) return false;

        return ApiKeyHelper.VerifyApiKey(apiKey, machine.ApiKeyHash);
    }
}