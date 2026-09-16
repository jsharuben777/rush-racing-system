using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using RushRacing.Core.Helpers;
using RushRacing.Core.Interfaces;
using RushRacing.Core.StateMachines;

namespace RushRacing.Data.Services;

public class SessionService : ISessionService
{
    private readonly RushRacingDbContext _db;
    private readonly ISessionEventService _events;

    public SessionService(RushRacingDbContext db, ISessionEventService events)
    {
        _db = db;
        _events = events;
    }

    public async Task<SessionCreateResult> CreateSession(
        int machineId, int pricingPlanId)
    {
        // Use a transaction to prevent double-booking
        await using var transaction = await _db.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            // Check machine exists and is enabled
            var machine = await _db.Machines
                .FirstOrDefaultAsync(m => m.MachineId == machineId && m.IsEnabled);

            if (machine == null)
                return SessionCreateResult.Fail("Machine not found or disabled.");

            if (machine.Status != MachineStatus.Ready)
                return SessionCreateResult.Fail("Machine is not available.");

            // Check no existing active session
            var existingSession = await _db.Sessions
                .Where(s => s.MachineId == machineId)
                .Where(s => s.Status != SessionStatus.Completed
                         && s.Status != SessionStatus.PaymentFailed
                         && s.Status != SessionStatus.AbandonedPayment
                         && s.Status != SessionStatus.AbandonedNoSelection
                         && s.Status != SessionStatus.Cancelled
                         && s.Status != SessionStatus.Refunded
                         && s.Status != SessionStatus.Expired)
                .FirstOrDefaultAsync();

            if (existingSession != null)
                return SessionCreateResult.Fail("Machine already has an active session.");

            // Get pricing plan
            var plan = await _db.PricingPlans
                .FirstOrDefaultAsync(p => p.PricingPlanId == pricingPlanId && p.IsActive);

            if (plan == null)
                return SessionCreateResult.Fail("Invalid pricing plan.");

            // Generate unique session code
            string sessionCode;
            do
            {
                sessionCode = SessionCodeGenerator.Generate();
            } while (await _db.Sessions.AnyAsync(s => s.SessionCode == sessionCode));

            // Create session
            var session = new Session
            {
                SessionCode = sessionCode,
                MachineId = machineId,
                PricingPlanId = pricingPlanId,
                DurationMinutes = plan.DurationMinutes,
                PriceAmount = plan.PriceAmount,
                Currency = plan.Currency,
                Status = SessionStatus.Created,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _db.Sessions.Add(session);

            // Reserve the machine
            machine.Status = MachineStatus.Reserved;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await _events.Log(session.SessionId, machineId,
                "SESSION_CREATED",
                $"Session {sessionCode} created. Duration: {plan.DurationMinutes}min, Price: {plan.PriceAmount} {plan.Currency}",
                "SERVER");

            return SessionCreateResult.Ok(session);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Session?> GetBySessionCode(string sessionCode)
    {
        return await _db.Sessions
            .Include(s => s.Machine)
            .Include(s => s.PricingPlan)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
    }

    public async Task<Session?> GetActiveSessionForMachine(int machineId)
    {
        return await _db.Sessions
            .Where(s => s.MachineId == machineId)
            .Where(s => s.Status == SessionStatus.Paid
                     || s.Status == SessionStatus.Starting
                     || s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> TransitionStatus(
        string sessionCode, SessionStatus newStatus)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session == null) return false;

        SessionStateMachine.ValidateTransition(session.Status, newStatus);

        session.Status = newStatus;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        await _events.Log(session.SessionId, session.MachineId,
            "STATUS_CHANGED",
            $"Session status changed to {newStatus}",
            "SERVER");

        return true;
    }

    public async Task MarkGameSelected(string sessionCode, string gameId)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session == null) return;

        session.SelectedGameId = gameId;
        session.GameSelectedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        await _events.Log(session.SessionId, session.MachineId,
            "GAME_SELECTED",
            $"Customer selected game: {gameId}",
            "AGENT");
    }

    public async Task MarkSessionStarted(string sessionCode)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session == null) return;

        SessionStateMachine.ValidateTransition(session.Status, SessionStatus.Active);

        session.Status = SessionStatus.Active;
        session.SessionStartedAt = DateTimeOffset.UtcNow;
        session.SessionExpiresAt = DateTimeOffset.UtcNow
            .AddMinutes(session.DurationMinutes);
        session.MachineAcknowledgedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        await _events.Log(session.SessionId, session.MachineId,
            "SESSION_STARTED",
            $"Session started. Expires at {session.SessionExpiresAt:HH:mm:ss}",
            "SERVER");
    }

    public async Task MarkSessionCompleted(string sessionCode)
    {
        var session = await _db.Sessions
            .Include(s => s.Machine)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session == null) return;

        session.Status = SessionStatus.Completed;
        session.SessionEndedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        // Free the machine
        session.Machine.Status = MachineStatus.Resetting;

        await _db.SaveChangesAsync();

        await _events.Log(session.SessionId, session.MachineId,
            "SESSION_COMPLETED",
            "Session completed normally",
            "SERVER");
    }

    public async Task MarkSessionError(string sessionCode, string reason)
    {
        var session = await _db.Sessions
            .Include(s => s.Machine)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session == null) return;

        session.Status = SessionStatus.MachineError;
        session.SessionEndedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        session.Machine.Status = MachineStatus.Error;

        await _db.SaveChangesAsync();

        await _events.Log(session.SessionId, session.MachineId,
            "SESSION_ERROR",
            $"Session error: {reason}",
            "SERVER");
    }
}