using RushRacing.Core.Entities;
using RushRacing.Core.Interfaces;

namespace RushRacing.Data.Services;

public class SessionEventService : ISessionEventService
{
    private readonly RushRacingDbContext _db;

    public SessionEventService(RushRacingDbContext db)
    {
        _db = db;
    }

    public async Task Log(int? sessionId, int? machineId, string eventType,
                          string? details, string source)
    {
        var evt = new SessionEvent
        {
            SessionId = sessionId,
            MachineId = machineId,
            EventType = eventType,
            Details = details,
            Source = source,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.SessionEvents.Add(evt);
        await _db.SaveChangesAsync();
    }
}