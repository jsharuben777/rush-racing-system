using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using RushRacing.Core.Interfaces;
using RushRacing.Data;
using System.Security.Claims;

namespace RushRacing.Web.Controllers;

[Route("admin")]
public class AdminController : Controller
{
    private readonly RushRacingDbContext _db;
    private readonly IMachineService _machineService;
    private readonly ISessionService _sessionService;
    private readonly IAuditService _auditService;

    public AdminController(
        RushRacingDbContext db,
        IMachineService machineService,
        ISessionService sessionService,
        IAuditService auditService)
    {
        _db = db;
        _machineService = machineService;
        _sessionService = sessionService;
        _auditService = auditService;
    }

    // ==================== LOGIN ====================

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard");

        return View();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(string username, string password)
    {
        var admin = await _db.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == username && a.IsActive);

        if (admin == null || !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.NameIdentifier, admin.AdminUserId.ToString()),
            new Claim("DisplayName", admin.DisplayName),
            new Claim(ClaimTypes.Role, admin.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, "AdminCookie");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("AdminCookie", principal);

        await _auditService.Log(admin.AdminUserId, "LOGIN", "AdminUser",
            admin.AdminUserId.ToString(), "Admin logged in",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return RedirectToAction("Dashboard");
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminCookie");
        return RedirectToAction("Login");
    }

    // ==================== DASHBOARD ====================

    [HttpGet("")]
    [HttpGet("dashboard")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTimeOffset.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // Machines
        var machines = await _db.Machines
            .Include(m => m.Location)
            .OrderBy(m => m.MachineCode)
            .ToListAsync();

        // Active sessions
        var activeSessions = await _db.Sessions
            .Include(s => s.Machine)
            .Include(s => s.SelectedGame)
            .Where(s => s.Status == SessionStatus.Active
                     || s.Status == SessionStatus.Starting)
            .ToListAsync();

        // Today's stats
        var todaySessions = await _db.Sessions
            .Where(s => s.CreatedAt >= today)
            .Where(s => s.Status == SessionStatus.Completed
                     || s.Status == SessionStatus.Active)
            .ToListAsync();

        var todayRevenue = await _db.Payments
            .Where(p => p.PaidAt >= today)
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => p.Amount);

        // This week
        var weekRevenue = await _db.Payments
            .Where(p => p.PaidAt >= weekStart)
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => p.Amount);

        // This month
        var monthRevenue = await _db.Payments
            .Where(p => p.PaidAt >= monthStart)
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => p.Amount);

        // Recent events
        var recentEvents = await _db.SessionEvents
            .Include(e => e.Machine)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.Machines = machines;
        ViewBag.ActiveSessions = activeSessions;
        ViewBag.TodaySessions = todaySessions.Count;
        ViewBag.TodayRevenue = todayRevenue;
        ViewBag.WeekRevenue = weekRevenue;
        ViewBag.MonthRevenue = monthRevenue;
        ViewBag.RecentEvents = recentEvents;

        return View();
    }

    // ==================== SESSIONS ====================

    [HttpGet("sessions")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> Sessions(string? status, int page = 1)
    {
        var query = _db.Sessions
            .Include(s => s.Machine)
            .Include(s => s.SelectedGame)
            .Include(s => s.Payments)
            .OrderByDescending(s => s.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<SessionStatus>(status, out var parsed))
            {
                query = query.Where(s => s.Status == parsed);
            }
        }

        var pageSize = 20;
        var totalCount = await query.CountAsync();
        var sessions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Sessions = sessions;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.StatusFilter = status;

        return View();
    }

    // ==================== SESSION DETAIL ====================

    [HttpGet("session/{sessionCode}")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> SessionDetail(string sessionCode)
    {
        var session = await _db.Sessions
            .Include(s => s.Machine)
            .Include(s => s.SelectedGame)
            .Include(s => s.PricingPlan)
            .Include(s => s.Payments)
            .Include(s => s.Events.OrderByDescending(e => e.CreatedAt))
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session == null)
            return NotFound();

        return View(session);
    }

    // ==================== MACHINES ====================

    [HttpGet("machines")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> Machines()
    {
        var machines = await _db.Machines
            .Include(m => m.Location)
            .OrderBy(m => m.MachineCode)
            .ToListAsync();

        // Get latest heartbeat for each machine
        var heartbeats = new Dictionary<int, MachineHeartbeat?>();
        foreach (var machine in machines)
        {
            var latest = await _db.MachineHeartbeats
                .Where(h => h.MachineId == machine.MachineId)
                .OrderByDescending(h => h.ReceivedAt)
                .FirstOrDefaultAsync();
            heartbeats[machine.MachineId] = latest;
        }

        ViewBag.Machines = machines;
        ViewBag.Heartbeats = heartbeats;

        return View();
    }

    // ==================== MACHINE DETAIL ====================

    [HttpGet("machine/{machineCode}")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> MachineDetail(string machineCode)
    {
        var machine = await _db.Machines
            .Include(m => m.Location)
            .FirstOrDefaultAsync(m => m.MachineCode == machineCode);

        if (machine == null)
            return NotFound();

        // Latest heartbeat
        var latestHeartbeat = await _db.MachineHeartbeats
            .Where(h => h.MachineId == machine.MachineId)
            .OrderByDescending(h => h.ReceivedAt)
            .FirstOrDefaultAsync();

        // Active session
        var activeSession = await _db.Sessions
            .Include(s => s.SelectedGame)
            .Where(s => s.MachineId == machine.MachineId)
            .Where(s => s.Status == SessionStatus.Active
                     || s.Status == SessionStatus.Starting)
            .FirstOrDefaultAsync();

        // Today's sessions for this machine
        var today = DateTimeOffset.UtcNow.Date;
        var todaySessions = await _db.Sessions
            .Where(s => s.MachineId == machine.MachineId)
            .Where(s => s.CreatedAt >= today)
            .Where(s => s.Status == SessionStatus.Completed)
            .CountAsync();

        var todayRevenue = await _db.Payments
            .Include(p => p.Session)
            .Where(p => p.Session.MachineId == machine.MachineId)
            .Where(p => p.PaidAt >= today)
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => p.Amount);

        // Recent events
        var recentEvents = await _db.SessionEvents
            .Where(e => e.MachineId == machine.MachineId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.Machine = machine;
        ViewBag.Heartbeat = latestHeartbeat;
        ViewBag.ActiveSession = activeSession;
        ViewBag.TodaySessions = todaySessions;
        ViewBag.TodayRevenue = todayRevenue;
        ViewBag.RecentEvents = recentEvents;

        return View();
    }

    // ==================== EVENTS LOG ====================

    [HttpGet("events")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> Events(int page = 1)
    {
        var pageSize = 50;

        var totalCount = await _db.SessionEvents.CountAsync();

        var events = await _db.SessionEvents
            .Include(e => e.Session)
            .Include(e => e.Machine)
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Events = events;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return View();
    }

    // ==================== REVENUE ====================

    [HttpGet("revenue")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> Revenue()
    {
        var today = DateTimeOffset.UtcNow.Date;

        // Last 7 days revenue
        var dailyRevenue = new List<object>();
        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var nextDate = date.AddDays(1);

            var revenue = await _db.Payments
                .Where(p => p.PaidAt >= date && p.PaidAt < nextDate)
                .Where(p => p.Status == PaymentStatus.Success)
                .SumAsync(p => p.Amount);

            var sessions = await _db.Sessions
                .Where(s => s.PaidAt >= date && s.PaidAt < nextDate)
                .Where(s => s.Status == SessionStatus.Completed
                         || s.Status == SessionStatus.Active)
                .CountAsync();

            dailyRevenue.Add(new
            {
                Date = date.ToString("ddd dd/MM"),
                Revenue = revenue,
                Sessions = sessions
            });
        }

        // Most popular duration
        var popularDuration = await _db.Sessions
            .Where(s => s.Status == SessionStatus.Completed)
            .GroupBy(s => s.DurationMinutes)
            .Select(g => new { Duration = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefaultAsync();

        // Most popular game
        var popularGame = await _db.Sessions
            .Where(s => s.Status == SessionStatus.Completed)
            .Where(s => s.SelectedGameId != null)
            .GroupBy(s => s.SelectedGameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefaultAsync();

        ViewBag.DailyRevenue = dailyRevenue;
        ViewBag.PopularDuration = popularDuration;
        ViewBag.PopularGame = popularGame;

        return View();
    }
}