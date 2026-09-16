using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using RushRacing.Core.Interfaces;
using RushRacing.Data;
using RushRacing.Web.Hubs;

namespace RushRacing.Web.Controllers.Api;

[ApiController]
[Route("api/payment")]
public class PaymentSimulationController : ControllerBase
{
    private readonly RushRacingDbContext _db;
    private readonly ISessionService _sessionService;
    private readonly ISessionEventService _events;
    private readonly IHubContext<MachineHub> _hubContext;


    public PaymentSimulationController(
        RushRacingDbContext db,
        ISessionService sessionService,
        ISessionEventService events,
        IHubContext<MachineHub> hubContext)
    {
        _db = db;
        _sessionService = sessionService;
        _events = events;
        _hubContext = hubContext;
    }

    /// <summary>
    /// DEVELOPMENT ONLY — Simulates a successful payment.
    /// This will be replaced by a real payment webhook handler.
    /// </summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> SimulatePayment([FromForm] string sessionCode)
    {
        var session = await _sessionService.GetBySessionCode(sessionCode);

        if (session == null)
            return NotFound("Session not found.");

        if (session.Status != SessionStatus.PendingPayment)
            return BadRequest("Session is not awaiting payment.");

        // Create a payment record
        var payment = new Payment
        {
            SessionId = session.SessionId,
            ExternalPaymentId = $"SIM-{Guid.NewGuid():N}",
            PaymentProvider = "SIMULATOR",
            Amount = session.PriceAmount,
            Currency = session.Currency,
            Status = PaymentStatus.Success,
            PaidAt = DateTimeOffset.UtcNow,
            RawWebhookPayload = "Simulated payment for development",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Transition session to PAID
        await _sessionService.TransitionStatus(sessionCode, SessionStatus.Paid);

        session = await _sessionService.GetBySessionCode(sessionCode);
        session!.PaidAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await _events.Log(session.SessionId, session.MachineId,
            "PAYMENT_RECEIVED",
            $"Simulated payment {payment.ExternalPaymentId} — RM{payment.Amount}",
            "WEBHOOK");

        // TODO: Signal the machine via SignalR to start the session
        // For now, we will add this in the SignalR step

        // Redirect customer to status page



        // Signal the machine to start the session
        await _sessionService.TransitionStatus(sessionCode, SessionStatus.Starting);

        await MachineHub.SendStartSession(
            _hubContext,
            session.MachineId,
            session.SessionCode,
            session.DurationMinutes);


        return Redirect($"/race/status/{sessionCode}");
    }
}