using RushRacing.Core.Entities;
using RushRacing.Core.Enums;

namespace RushRacing.Core.Interfaces;

public interface IMachineService
{
    Task<Machine?> GetBySlug(string slug);
    Task<Machine?> GetByMachineCode(string machineCode);
    Task<Machine?> GetById(int machineId);
    Task<List<Machine>> GetAllWithStatus();
    Task<bool> UpdateStatus(int machineId, MachineStatus newStatus);
    Task RecordHeartbeat(int machineId, MachineHeartbeat heartbeat);
    Task<bool> IsMachineAvailable(int machineId);
    Task<bool> AuthenticateMachine(string machineCode, string apiKey);
}