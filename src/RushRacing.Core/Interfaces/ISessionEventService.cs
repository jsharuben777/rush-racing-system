namespace RushRacing.Core.Interfaces;

public interface ISessionEventService
{
    Task Log(int? sessionId, int? machineId, string eventType,
             string? details, string source);
}