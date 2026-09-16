using RushRacing.Core.Entities;
using RushRacing.Core.Enums;

namespace RushRacing.Core.Interfaces;

public interface ISessionService
{
    Task<SessionCreateResult> CreateSession(int machineId, int pricingPlanId);
    Task<Session?> GetBySessionCode(string sessionCode);
    Task<Session?> GetActiveSessionForMachine(int machineId);
    Task<bool> TransitionStatus(string sessionCode, SessionStatus newStatus);
    Task MarkGameSelected(string sessionCode, string gameId);
    Task MarkSessionStarted(string sessionCode);
    Task MarkSessionCompleted(string sessionCode);
    Task MarkSessionError(string sessionCode, string reason);
}

public class SessionCreateResult
{
    public bool Success { get; set; }
    public string? SessionCode { get; set; }
    public Session? Session { get; set; }
    public string? ErrorMessage { get; set; }

    public static SessionCreateResult Ok(Session session) =>
        new() { Success = true, SessionCode = session.SessionCode, Session = session };

    public static SessionCreateResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}