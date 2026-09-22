using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using RushRacing.Core.Interfaces;
using RushRacing.Data;
using RushRacing.Web.Services;

namespace RushRacing.Web.Controllers;

public class BookingController : Controller
{
    private readonly RushRacingDbContext _db;
    private readonly ISessionService _sessionService;
    private readonly IMachineService _machineService;
    private readonly IBillplzService _billplzService;
    private readonly IConfiguration _configuration;

    public BookingController(
        RushRacingDbContext db,
        ISessionService sessionService,
        IMachineService machineService,
        IBillplzService billplzService,
        IConfiguration configuration)
    {
        _db = db;
        _sessionService = sessionService;
        _machineService = machineService;
        _billplzService = billplzService;
        _configuration = configuration;
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
        ViewBag.Error = TempData["Error"];

        return View(session);
    }

    // POST: /race/payment/RR82931/create-bill
    // Creates a Billplz Bill for this session and sends the customer to Billplz's hosted payment page.
    [HttpPost("race/payment/{sessionCode}/create-bill")]
    public async Task<IActionResult> CreateBill(string sessionCode, string email)
    {
        var session = await _sessionService.GetBySessionCode(sessionCode);

        if (session == null)
            return View("SessionNotFound");

        if (session.Status != SessionStatus.PendingPayment)
            return View("SessionInvalid");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            TempData["Error"] = "Please enter a valid email address.";
            return RedirectToAction("Payment", new { sessionCode });
        }

        session.CustomerEmail = email;
        await _db.SaveChangesAsync();

        var baseUrl = _configuration["RushRacing:PublicBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            // This means the tunnel URL hasn't been patched into appsettings.json —
            // almost always because the Launcher wasn't used to start this app.
            return StatusCode(500,
                "Server is not configured with a public URL (RushRacing:PublicBaseUrl in appsettings.json). " +
                "Start this app via the Launcher so the tunnel URL gets patched in automatically.");
        }

        var callbackUrl = $"{baseUrl}/webhooks/billplz/callback";
        var redirectUrl = $"{baseUrl}/race/payment/{sessionCode}/redirect";

        BillplzBill bill;
        try
        {
            bill = await _billplzService.CreateBillAsync(session, email, callbackUrl, redirectUrl);
        }
        catch (Exception)
        {
            TempData["Error"] = "Could not reach the payment gateway. Please try again in a moment.";
            // In a real deployment, log this exception via ILogger.
            return RedirectToAction("Payment", new { sessionCode });
        }

        var payment = new Payment
        {
            SessionId = session.SessionId,
            ExternalPaymentId = bill.Id,
            PaymentProvider = "BILLPLZ",
            Amount = session.PriceAmount,
            Currency = session.Currency,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return Redirect(bill.Url);
    }

    // GET: /race/payment/RR82931/redirect
    // Billplz sends the customer's browser here after they finish paying.
    // This is for UX only — the webhook callback is the real source of truth.
    [HttpGet("race/payment/{sessionCode}/redirect")]
    public IActionResult PaymentRedirect(string sessionCode)
    {
        // Verified only for logging purposes here; not used to decide anything,
        // since the callback (server-to-server) already handles the real status update.
        _ = _billplzService.VerifyQuerySignature(Request.Query);

        return RedirectToAction("Status", new { sessionCode });
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
