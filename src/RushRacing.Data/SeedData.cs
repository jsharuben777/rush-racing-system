using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Entities;
using RushRacing.Core.Enums;
using System.Security.Cryptography;
using System.Text;

namespace RushRacing.Data;

public static class SeedData
{
    public static async Task SeedAsync(RushRacingDbContext db)
    {
        if (await db.Locations.AnyAsync())
            return; // Already seeded

        // Location
        var location = new Location
        {
            Name = "Test Location",
            Address = "Development Environment",
            TimeZone = "Asia/Kuala_Lumpur",
            IsActive = true
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        // Machine
        // Generate an API key for the test machine
        //var apiKey = GenerateApiKey();

        var apiKey = "RushRacingTestKey123456789";
        var machine = new Machine
        {
            LocationId = location.LocationId,
            MachineCode = "RR-01",
            DisplayName = "Cockpit 01",
            Slug = "ck01",
            ApiKeyHash = HashApiKey(apiKey),
            Status = MachineStatus.Offline,
            IsEnabled = true
        };
        db.Machines.Add(machine);

        // Games
        db.Games.AddRange(
            new Game
            {
                GameId = "acc",
                DisplayName = "Assetto Corsa Competizione",
                ImageFileName = "acc.jpg",
                IsActive = true,
                DisplayOrder = 1
            },
            new Game
            {
                GameId = "f1_2025",
                DisplayName = "F1 2025",
                ImageFileName = "f1_2025.jpg",
                IsActive = true,
                DisplayOrder = 2
            }
        );



        // Pricing Plans

        db.PricingPlans.AddRange(

            new PricingPlan
            {
                LocationId = location.LocationId,
                DurationMinutes = 1,
                PriceAmount = 1.00m,
                Currency = "MYR",
                IsActive = true,
                DisplayOrder = 0
            },
            new PricingPlan
            {
                LocationId = location.LocationId,
                DurationMinutes = 2,
                PriceAmount = 2.00m,
                Currency = "MYR",
                IsActive = true,
                DisplayOrder = 1
            },
            new PricingPlan
            {
                LocationId = location.LocationId,
                DurationMinutes = 10,
                PriceAmount = 10.00m,
                Currency = "MYR",
                IsActive = true,
                DisplayOrder = 2
            },
            new PricingPlan
            {
                LocationId = location.LocationId,
                DurationMinutes = 20,
                PriceAmount = 18.00m,
                Currency = "MYR",
                IsActive = true,
                DisplayOrder = 3
            },
            new PricingPlan
            {
                LocationId = location.LocationId,
                DurationMinutes = 30,
                PriceAmount = 25.00m,
                Currency = "MYR",
                IsActive = true,
                DisplayOrder = 4
            }
        );

        // Admin Users
        db.AdminUsers.AddRange(
            new AdminUser
            {
                Username = "jay",
                PasswordHash = HashPassword("123"),
                DisplayName = "Jay",
                Role = AdminRole.Admin,
                IsActive = true
            },
            new AdminUser
            {
                Username = "kishen",
                PasswordHash = HashPassword("123"),
                DisplayName = "Kishen",
                Role = AdminRole.Admin,
                IsActive = true
            }
        );

        await db.SaveChangesAsync();

        // Output the API key to console so it can be saved
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║  SEED DATA CREATED SUCCESSFULLY             ║");
        Console.WriteLine("╠══════════════════════════════════════════════╣");
        Console.WriteLine($"║  Machine: {machine.MachineCode,-33} ║");
        Console.WriteLine($"║  API Key: {apiKey,-33} ║");
        Console.WriteLine("║                                              ║");
        Console.WriteLine("║  ⚠ SAVE THIS API KEY NOW                    ║");
        Console.WriteLine("║  It cannot be retrieved later.               ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string HashApiKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }

    private static string HashPassword(string password)
    {
        // For MVP, use a simple hash. Replace with BCrypt/Argon2 for production.
        return BCryptHash(password);
    }

    private static string BCryptHash(string password)
    {
        // We will add BCrypt NuGet package
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
