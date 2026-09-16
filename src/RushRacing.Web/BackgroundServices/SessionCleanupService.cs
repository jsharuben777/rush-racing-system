using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Enums;
using RushRacing.Data;

namespace RushRacing.Web.BackgroundServices;

public class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAbandonedPayments();
                await CleanupExpiredSessions();
                await CleanupStaleHeartbeats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in session cleanup service.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CleanupAbandonedPayments()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushRacingDbContext>();

        var threshold = DateTimeOffset.UtcNow.AddMinutes(-10);

        var abandonedSessions = await db.Sessions
            .Include(s => s.Machine)
            .Where(s => s.Status == SessionStatus.PendingPayment
                     || s.Status == SessionStatus.Created)
            .Where(s => s.CreatedAt < threshold)
            .ToListAsync();

        foreach (var session in abandonedSessions)
        {
            session.Status = SessionStatus.AbandonedPayment;
            session.UpdatedAt = DateTimeOffset.UtcNow;

            // Free the machine if it was reserved
            if (session.Machine.Status == MachineStatus.Reserved)
            {
                session.Machine.Status = MachineStatus.Ready;
            }

            _logger.LogInformation(
                "Abandoned session {SessionCode} — no payment received within timeout.",
                session.SessionCode);
        }

        if (abandonedSessions.Any())
        {
            await db.SaveChangesAsync();
        }
    }

    private async Task CleanupExpiredSessions()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushRacingDbContext>();

        // Find sessions in STARTING that have been stuck for too long
        // (game selection timeout — no game selected within 5 minutes)
        var startingThreshold = DateTimeOffset.UtcNow.AddMinutes(-5);

        var stuckSessions = await db.Sessions
            .Include(s => s.Machine)
            .Where(s => s.Status == SessionStatus.Starting)
            .Where(s => s.PaidAt < startingThreshold)
            .ToListAsync();

        foreach (var session in stuckSessions)
        {
            session.Status = SessionStatus.AbandonedNoSelection;
            session.UpdatedAt = DateTimeOffset.UtcNow;

            if (session.Machine.Status != MachineStatus.Offline
                && session.Machine.Status != MachineStatus.Error)
            {
                session.Machine.Status = MachineStatus.Ready;
            }

            _logger.LogWarning(
                "Session {SessionCode} abandoned — no game selection within timeout.",
                session.SessionCode);
        }

        if (stuckSessions.Any())
        {
            await db.SaveChangesAsync();
        }
    }

    private async Task CleanupStaleHeartbeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushRacingDbContext>();

        // Delete heartbeats older than 7 days to prevent table bloat
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);

        var staleHeartbeats = await db.MachineHeartbeats
     .Where(h => h.ReceivedAt < cutoff)
     .ToListAsync();

        if (staleHeartbeats.Any())
        {
            db.MachineHeartbeats.RemoveRange(staleHeartbeats);
            await db.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} stale heartbeat records.", staleHeartbeats.Count);
        }
    }
}