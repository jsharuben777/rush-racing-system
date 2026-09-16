using System.Security.Cryptography;

namespace RushRacing.Core.Helpers;

public static class SessionCodeGenerator
{
    /// <summary>
    /// Generates a human-readable session code like "RR84291"
    /// </summary>
    public static string Generate()
    {
        var number = RandomNumberGenerator.GetInt32(10000, 99999);
        return $"RR{number}";
    }
}