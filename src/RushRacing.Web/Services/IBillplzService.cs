using Microsoft.AspNetCore.Http;
using RushRacing.Core.Entities;

namespace RushRacing.Web.Services;

public interface IBillplzService
{
    Task<BillplzBill> CreateBillAsync(Session session, string email, string callbackUrl, string redirectUrl);

    /// <summary>
    /// Verifies the X-Signature on a server-to-server callback (POST form body).
    /// This is the authoritative check — always trust this over the redirect.
    /// </summary>
    bool VerifyFormSignature(IFormCollection form);

    /// <summary>
    /// Verifies the X-Signature on the customer's browser redirect (GET query string).
    /// Use only for UX / logging — never as the source of truth for payment status.
    /// </summary>
    bool VerifyQuerySignature(IQueryCollection query);
}

public class BillplzBill
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
