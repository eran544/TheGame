namespace Flip7Server.Models;

/// <summary>
/// A seat in a Flip 7 game. Humans reference their Auth-service identity by
/// <see cref="UserId"/>; AI players carry a stable AI id plus a per-player
/// <see cref="AiStyle"/>/<see cref="AiDifficulty"/> chosen in the lobby.
/// <see cref="Username"/> is denormalized from the JWT (no User projection).
/// </summary>
public class Flip7Player
{
    public Guid Id { get; set; }

    public Guid GameSessionId { get; set; }
    public Flip7GameSession GameSession { get; set; } = null!;

    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Turn order within the game (0-based).</summary>
    public int Seat { get; set; }

    public bool IsAi { get; set; }

    /// <summary>"safe" | "balanced" | "risky" — AI only.</summary>
    public string? AiStyle { get; set; }

    /// <summary>"easy" | "medium" | "hard" — AI only.</summary>
    public string? AiDifficulty { get; set; }

    /// <summary>Running total across rounds; first to <c>TargetScore</c> triggers game end.</summary>
    public int CumulativeScore { get; set; }
}
