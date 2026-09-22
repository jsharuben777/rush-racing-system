using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Enums;
using RushRacing.Core.Interfaces;
using RushRacing.Data;
using RushRacing.Web.Hubs;
using RushRacing.Web.Services;

namespace RushRacing.Web.Controllers.Api;

[ApiController]
[Route("webhooks/billplz")]
public class BillplzWebhookController : ControllerBase
{
    private readonly RushRacingDbContext _db;
    private readonly ISessionService _sessionService;
    private readonly ISessionEventService _events;
    private readonly IHubContext<MachineHub> _hubContext;
    private readonly IBillplzService _billplz;
    private readonly ILogger<BillplzWebhookController> _logger;

    public BillplzWebhookController(
        RushRacingDbContext db,
        ISessionService sessionService,
        ISessionEventService events,
        IHubContext<MachineHub> hubContext,
        IBillplzService billplz,
        ILogger<BillplzWebhookController> logger)
    {
        _db = db;
        _sessionService = sessionService;
        _events = events;
        _hubContext = hubContext;
        _billplz = billplz;
        _logger = logger;
    }

    /// <summary>
    /// Server-to-server callback from Billplz. This is the authoritative source of
    /// truth for payment status — never rely solely on the customer's browser redirect.
    /// </summary>
    // POST: /webhooks/billplz/callback
    [HttpPost("callback")]
    public async Task<IActionResult> Callback()
    {
        if (!_billplz.VerifyFormSignature(Request.Form))
        {
            _logger.LogWarning("Billplz callback signature verification FAILED. Remote IP: {IP}",
                HttpContext.Connection.RemoteIpAddress);
            return BadRequest("Invalid signature.");
        }

        var billId = Request.Form["id"].ToString();
        var paid = Request.Form["paid"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        var sessionCode = Request.Form["reference_1"].ToString();

        if (string.IsNullOrEmpty(sessionCode))
        {
            _logger.LogWarning("Billplz callback missing reference_1 (session code). Bill: {BillId}", billId);
            return Ok(); // Acknowledge so Billplz doesn't keep retrying something we can't process.
        }

        var session = await _sessionService.GetBySessionCode(sessionCode);
        if (session == null)
        {
            _logger.LogWarning("Billplz callback for unknown session {SessionCode}. Bill: {BillId}", sessionCode, billId);
            return Ok();
        }

        var payment = await _db.Payments
            .Where(p => p.SessionId == session.SessionId && p.ExternalPaymentId == billId)
            .FirstOrDefaultAsync();

        if (payment == null)
        {
            _logger.LogWarning("Billplz callback: no matching Payment record for bill {BillId}, session {SessionCode}.",
                billId, sessionCode);
            return Ok();
        }

        // Idempotency guard — Billplz retries callbacks up to 5 times, and the redirect
        // can also race with the callback. Never re-process a payment already resolved.
        if (payment.Status is PaymentStatus.Success or PaymentStatus.Failed)
            return Ok();

        payment.RawWebhookPayload = string.Join("&",
            Request.Form.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value.ToString())}"));

        if (paid)
        {
            payment.Status = PaymentStatus.Success;
            payment.PaidAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            await _sessionService.TransitionStatus(sessionCode, SessionStatus.Paid);

            session = await _sessionService.GetBySessionCode(sessionCode);
            session!.PaidAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            await _events.Log(session.SessionId, session.MachineId,
                "PAYMENT_RECEIVED",
                $"Billplz payment {payment.ExternalPaymentId} — RM{payment.Amount}",
                "WEBHOOK");

            await _sessionService.TransitionStatus(sessionCode, SessionStatus.Starting);

            await MachineHub.SendStartSession(
                _hubContext,
                session.MachineId,
                session.SessionCode,
                session.DurationMinutes);
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync();

            await _sessionService.TransitionStatus(sessionCode, SessionStatus.PaymentFailed);

            await _events.Log(session.SessionId, session.MachineId,
                "PAYMENT_FAILED",
                $"Billplz payment {payment.ExternalPaymentId} failed or was not completed.",
                "WEBHOOK");
        }

        return Ok();
    }
}
