namespace AuthServer.Models;

/// <summary>
/// Aggregate, cross-game profile for a user. Per-game statistics stay in each
/// game's own database; this is the place to roll up platform-wide info.
/// </summary>
public class PlayerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? DisplayName { get; set; }
    public int TotalGamesPlayed { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
