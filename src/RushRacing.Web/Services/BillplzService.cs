using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RushRacing.Core.Entities;

namespace RushRacing.Web.Services;

public class BillplzService : IBillplzService
{
    private readonly HttpClient _http;
    private readonly BillplzOptions _options;

    public BillplzService(HttpClient http, IOptions<BillplzOptions> options)
    {
        _http = http;
        _options = options.Value;

        var authBytes = Encoding.ASCII.GetBytes($"{_options.ApiSecretKey}:");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    public async Task<BillplzBill> CreateBillAsync(Session session, string email, string callbackUrl, string redirectUrl)
    {
        var amountInCents = (int)Math.Round(session.PriceAmount * 100m, MidpointRounding.AwayFromZero);

        var form = new Dictionary<string, string>
        {
            ["collection_id"] = _options.CollectionId,
            ["email"] = email,
            ["name"] = "Rush Racing Customer",
            ["amount"] = amountInCents.ToString(),
            ["callback_url"] = callbackUrl,
            ["redirect_url"] = redirectUrl,
            ["description"] = $"Rush Racing - {session.DurationMinutes} min session",
            ["reference_1_label"] = "Session Code",
            ["reference_1"] = session.SessionCode
        };

        var response = await _http.PostAsync(
            $"{_options.BaseUrl}bills",
            new FormUrlEncodedContent(form));

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Billplz create bill failed ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new BillplzBill
        {
            Id = root.GetProperty("id").GetString() ?? string.Empty,
            Url = root.GetProperty("url").GetString() ?? string.Empty
        };
    }

    public bool VerifyFormSignature(IFormCollection form)
    {
        if (!form.ContainsKey("x_signature"))
            return false;

        var providedSignature = form["x_signature"].ToString();

        var pairs = form
            .Where(kv => !string.Equals(kv.Key, "x_signature", StringComparison.OrdinalIgnoreCase))
            .Select(kv => (Key: kv.Key, Value: kv.Value.ToString()))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}{kv.Value}");

        var sourceString = string.Join("|", pairs);
        var computed = ComputeHmacSha256Hex(sourceString, _options.XSignatureKey);

        return SlowEquals(computed, providedSignature);
    }

    public bool VerifyQuerySignature(IQueryCollection query)
    {
        // Billplz sends nested keys like "billplz[id]" — normalize by stripping brackets,
        // per Billplz's documented rule for nested parameters in the source string.
        string Normalize(string key) => key.Replace("[", "").Replace("]", "");

        var signatureKey = query.Keys.FirstOrDefault(k =>
            Normalize(k).Equals("billplzx_signature", StringComparison.OrdinalIgnoreCase));

        if (signatureKey == null)
            return false;

        var providedSignature = query[signatureKey].ToString();

        var pairs = query
            .Where(kv => kv.Key != signatureKey)
            .Select(kv => (Key: Normalize(kv.Key), Value: kv.Value.ToString()))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}{kv.Value}");

        var sourceString = string.Join("|", pairs);
        var computed = ComputeHmacSha256Hex(sourceString, _options.XSignatureKey);

        return SlowEquals(computed, providedSignature);
    }

    private static string ComputeHmacSha256Hex(string message, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Constant-time string comparison, to avoid leaking timing information about
    /// how much of the signature matched.
    /// </summary>
    private static bool SlowEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }
}
