using System.Security.Cryptography;
using System.Text;

namespace RushRacing.Core.Helpers;

public static class ApiKeyHelper
{
    public static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string HashApiKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }

    public static bool VerifyApiKey(string apiKey, string storedHash)
    {
        var hash = HashApiKey(apiKey);
        return string.Equals(hash, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}