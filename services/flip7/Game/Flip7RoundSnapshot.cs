namespace Flip7Server.Game;

/// <summary>
/// A serializable snapshot of a <see cref="Flip7Round"/>'s full state. Because
/// the round draws deterministically from the front of the draw pile, capturing
/// the remaining pile, discard, and every line reconstructs the round exactly.
/// The RNG (used only for the rare mid-round reshuffle) is intentionally not
/// persisted — a fresh one is used on restore.
/// </summary>
public sealed class Flip7RoundSnapshot
{
    public List<Guid> TurnOrder { get; set; } = new();
    public List<PlayerLineSnapshot> Lines { get; set; } = new();
    public List<Flip7Card> DrawPile { get; set; } = new();
    public List<Flip7Card> Discard { get; set; } = new();
    public int CurrentIndex { get; set; } = -1;
    public bool Dealt { get; set; }
    public bool RoundEnded { get; set; }
    public RoundEndReason EndReason { get; set; } = RoundEndReason.None;

    /// <summary>Drawn-but-unresolved action cards, head first.</summary>
    public List<PendingActionSnapshot> ActionQueue { get; set; } = new();

    /// <summary>Seat index the initial deal is paused at; -1 when not dealing.</summary>
    public int DealCursor { get; set; } = -1;

    public bool AdvanceTurnAfterResolve { get; set; }

    /// <summary>Candidates of the suspended queue head; null when nothing is pending.</summary>
    public List<Guid>? PendingCandidates { get; set; }
}

public sealed class PendingActionSnapshot
{
    public ActionKind Action { get; set; }
    public Guid DrawerId { get; set; }
}

public sealed class PlayerLineSnapshot
{
    public Guid PlayerId { get; set; }
    public List<int> Numbers { get; set; } = new();
    public List<ModifierKind> Modifiers { get; set; } = new();
    public bool HasSecondChance { get; set; }
    public PlayerLineStatus Status { get; set; } = PlayerLineStatus.Active;
    public bool AchievedFlip7 { get; set; }

    /// <summary>The duplicate that busted this line; null unless the line busted.</summary>
    public int? BustedNumber { get; set; }
}
