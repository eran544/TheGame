namespace Flip7Server.Game;

/// <summary>What happened during card resolution — surfaced for hub broadcasts and tests.</summary>
public enum Flip7EventType
{
    NumberAdded,
    ModifierAdded,
    Busted,
    SecondChanceGained,
    SecondChancePassed,
    SecondChanceUsed,
    SecondChanceDiscarded,
    Frozen,
    FlipThreeStarted,
    Flip7Achieved,
    Stayed,
    RoundEnded,
}

/// <summary>
/// An immutable record of a single resolution step. The engine returns an
/// ordered list of these from each <c>DealInitial</c>/<c>Hit</c>/<c>Stay</c>
/// call so callers can animate/broadcast exactly what occurred.
/// </summary>
public sealed record Flip7Event
{
    public required Flip7EventType Type { get; init; }

    /// <summary>The player primarily affected (e.g. who gained/lost a card, who was frozen).</summary>
    public Guid PlayerId { get; init; }

    /// <summary>The player who drew the card causing this event, when different (e.g. an action card's drawer).</summary>
    public Guid? SourcePlayerId { get; init; }

    public Flip7Card? Card { get; init; }

    public string? Detail { get; init; }
}
