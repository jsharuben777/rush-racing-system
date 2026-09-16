using RushRacing.Core.Entities;

namespace RushRacing.Core.Interfaces;

public interface IPaymentService
{
    Task<PaymentInitResult> InitiatePayment(string sessionCode);
    Task<bool> HandleWebhook(string provider, string payload, string signature);
}

public class PaymentInitResult
{
    public bool Success { get; set; }
    public string? PaymentUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public static PaymentInitResult Ok(string paymentUrl) =>
        new() { Success = true, PaymentUrl = paymentUrl };

    public static PaymentInitResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}