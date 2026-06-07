namespace Flip7Server.Game;

/// <summary>A player's standing within a single round.</summary>
public enum PlayerLineStatus
{
    /// <summary>Still in the round; a valid target for action cards.</summary>
    Active,

    /// <summary>Voluntarily exited and banked points.</summary>
    Stayed,

    /// <summary>Forced to bank and exit by a Freeze card.</summary>
    Frozen,

    /// <summary>Drew a duplicate number; scores 0 this round.</summary>
    Busted,
}

/// <summary>
/// One player's face-up line for the current round: number cards, modifiers, a
/// held Second Chance, and status. Number values are always unique among the
/// cards here — a duplicate either triggers a bust or is negated (and discarded)
/// by a Second Chance, so it never lands in <see cref="Numbers"/>.
/// </summary>
public sealed class PlayerLine
{
    public required Guid PlayerId { get; init; }

    public List<int> Numbers { get; } = new();
    public List<ModifierKind> Modifiers { get; } = new();
    public bool HasSecondChance { get; set; }
    public PlayerLineStatus Status { get; set; } = PlayerLineStatus.Active;
    public bool AchievedFlip7 { get; set; }

    public bool IsActive => Status == PlayerLineStatus.Active;

    /// <summary>Distinct number cards in the line (each is already unique).</summary>
    public int UniqueNumberCount => Numbers.Count;

    public bool HasNumber(int value) => Numbers.Contains(value);

    /// <summary>True once the line holds at least one card (required to Stay).</summary>
    public bool HasAnyCard => Numbers.Count > 0 || Modifiers.Count > 0;

    /// <summary>Banked score for this round: 0 if busted, else the computed line value.</summary>
    public int Score => Status == PlayerLineStatus.Busted
        ? 0
        : Flip7Scoring.Score(Numbers, Modifiers, AchievedFlip7);
}
