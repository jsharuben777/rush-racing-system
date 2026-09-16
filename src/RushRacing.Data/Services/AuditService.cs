using RushRacing.Core.Entities;
using RushRacing.Core.Interfaces;

namespace RushRacing.Data.Services;

public class AuditService : IAuditService
{
    private readonly RushRacingDbContext _db;

    public AuditService(RushRacingDbContext db)
    {
        _db = db;
    }

    public async Task Log(int? adminUserId, string action, string? targetType,
                          string? targetId, string? details, string? ipAddress)
    {
        var log = new AuditLog
        {
            AdminUserId = adminUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}