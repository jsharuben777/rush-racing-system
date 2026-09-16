using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using RushRacing.Core.Helpers;
using RushRacing.Core.Interfaces;
using RushRacing.Data;


namespace RushRacing.Web.Controllers.Api;

[ApiController]
[Route("api/machine")]
public class MachineApiController : ControllerBase
{
    private readonly IMachineService _machineService;
    private readonly RushRacingDbContext _db;

    public MachineApiController(IMachineService machineService, RushRacingDbContext db)
    {
        _machineService = machineService;
        _db = db;
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        // Authenticate
        var machineCode = Request.Headers["X-Machine-Code"].FirstOrDefault();
        var apiKey = Request.Headers["X-Machine-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(apiKey))
            return Unauthorized();

        if (!await _machineService.AuthenticateMachine(machineCode, apiKey))
            return Unauthorized();

        var machine = await _machineService.GetByMachineCode(machineCode);
        if (machine == null) return NotFound();

        var heartbeat = new MachineHeartbeat
        {
            MachineId = machine.MachineId,
            CpuPercent = request.CpuPercent,
            RamPercent = request.RamPercent,
            GpuTempCelsius = request.GpuTempCelsius,
            DiskFreeGb = request.DiskFreeGb,
            InternetStatus = request.InternetStatus,
            AgentVersion = request.AgentVersion,
            GameProcessRunning = request.GameProcessRunning,
            ActiveSessionCode = request.ActiveSessionCode,
            ActiveGameId = request.ActiveGameId
        };

        await _machineService.RecordHeartbeat(machine.MachineId, heartbeat);

        return Ok(new { received = true });
    }

    [HttpGet("session/{machineCode}")]
    public async Task<IActionResult> GetActiveSession(string machineCode)
    {
        var apiKey = Request.Headers["X-Machine-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey)) return Unauthorized();
        if (!await _machineService.AuthenticateMachine(machineCode, apiKey))
            return Unauthorized();

        var machine = await _machineService.GetByMachineCode(machineCode);
        if (machine == null) return NotFound();

        var session = await _db.Sessions
            .Where(s => s.MachineId == machine.MachineId)
            .Where(s => s.Status == SessionStatus.Paid
                     || s.Status == SessionStatus.Starting
                     || s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.SessionCode,
                s.SelectedGameId,
                s.SessionExpiresAt,
                Status = s.Status.ToString(),
                s.DurationMinutes
            })
            .FirstOrDefaultAsync();

        if (session == null)
            return Ok(new { hasActiveSession = false });

        return Ok(new { hasActiveSession = true, session });
    }
}

public class HeartbeatRequest
{
    public double? CpuPercent { get; set; }
    public double? RamPercent { get; set; }
    public double? GpuTempCelsius { get; set; }
    public double? DiskFreeGb { get; set; }
    public string? InternetStatus { get; set; }
    public string? AgentVersion { get; set; }
    public bool? GameProcessRunning { get; set; }
    public string? ActiveSessionCode { get; set; }
    public string? ActiveGameId { get; set; }
}