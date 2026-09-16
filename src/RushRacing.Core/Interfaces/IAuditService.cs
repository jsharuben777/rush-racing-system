namespace RushRacing.Core.Interfaces;

public interface IAuditService
{
    Task Log(int? adminUserId, string action, string? targetType,
             string? targetId, string? details, string? ipAddress);
}