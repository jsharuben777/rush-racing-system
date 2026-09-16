using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RushRacing.Core.Enums;
using RushRacing.Core.Interfaces;
using RushRacing.Data;

namespace RushRacing.Web.Controllers;

public class BookingController : Controller
{
    private readonly RushRacingDbContext _db;
    private readonly ISessionService _sessionService;
    private readonly IMachineService _machineService;

    public BookingController(
        RushRacingDbContext db,
        ISessionService sessionService,
        IMachineService machineService)
    {
        _db = db;
        _sessionService = sessionService;
        _machineService = machineService;
    }

    // GET: /race/ck01
    [HttpGet("race/{slug}")]
    public async Task<IActionResult> Index(string slug)
    {
        var machine = await _machineService.GetBySlug(slug);

        if (machine == null)
            return View("MachineNotFound");

        if (!machine.IsEnabled)
            return View("MachineUnavailable");

        if (machine.Status != MachineStatus.Ready)
            return View("MachineBusy", machine);

        // Get available pricing plans for this location
        var plans = await _db.PricingPlans
            .Where(p => p.IsActive)
            .Where(p => p.LocationId == machine.LocationId || p.LocationId == null)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        ViewBag.Machine = machine;
        ViewBag.Plans = plans;

        return View();
    }

    // POST: /race/ck01/book
    [HttpPost("race/{slug}/book")]
    public async Task<IActionResult> Book(string slug, int pricingPlanId)
    {
        var machine = await _machineService.GetBySlug(slug);

        if (machine == null)
            return View("MachineNotFound");

        if (!await _machineService.IsMachineAvailable(machine.MachineId))
            return View("MachineUnavailable");

        // Create session
        var result = await _sessionService.CreateSession(
            machine.MachineId, pricingPlanId);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction("Index", new { slug });
        }

        // Transition to pending payment
        await _sessionService.TransitionStatus(
            result.SessionCode!, SessionStatus.PendingPayment);

        // Redirect to payment
        return RedirectToAction("Payment", new { sessionCode = result.SessionCode });
    }

    // GET: /race/payment/RR82931
    [HttpGet("race/payment/{sessionCode}")]
    public async Task<IActionResult> Payment(string sessionCode)
    {
        var session = await _sessionService.GetBySessionCode(sessionCode);

        if (session == null)
            return View("SessionNotFound");

        if (session.Status != SessionStatus.PendingPayment)
            return View("SessionInvalid");

        ViewBag.Session = session;

        // For MVP: We will integrate with the payment provider here
        // For now, show a payment page placeholder
        return View(session);
    }

    // GET: /race/status/RR82931
    [HttpGet("race/status/{sessionCode}")]
    public async Task<IActionResult> Status(string sessionCode)
    {
        var session = await _sessionService.GetBySessionCode(sessionCode);

        if (session == null)
            return View("SessionNotFound");

        return View(session);
    }
}