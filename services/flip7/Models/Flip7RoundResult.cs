namespace Flip7Server.Models;

/// <summary>Per-player outcome of a completed round (history / stats).</summary>
public class Flip7RoundResult
{
    public Guid Id { get; set; }

    public Guid GameSessionId { get; set; }
    public Flip7GameSession GameSession { get; set; } = null!;

    public int RoundNumber { get; set; }

    /// <summary>The seat's <c>Flip7Player.Id</c>.</summary>
    public Guid PlayerId { get; set; }

    public int RoundScore { get; set; }
    public bool Busted { get; set; }
    public bool AchievedFlip7 { get; set; }

    /// <summary>The player's cumulative score after banking this round.</summary>
    public int CumulativeAfter { get; set; }
}
