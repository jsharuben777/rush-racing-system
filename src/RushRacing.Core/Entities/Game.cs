namespace RushRacing.Core.Entities;

public class Game
{
    public string GameId { get; set; } = string.Empty;            // "acc"
    public string DisplayName { get; set; } = string.Empty;       // "Assetto Corsa Competizione"
    public string? ImageFileName { get; set; }                    // "acc.jpg"
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}