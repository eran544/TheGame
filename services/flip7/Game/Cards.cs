namespace Flip7Server.Game;

/// <summary>The three families of Flip 7 cards.</summary>
public enum CardKind
{
    Number,
    Modifier,
    Action,
}

/// <summary>
/// Score Modifier cards. Placed above the number row; they never bust and never
/// count toward the Flip 7 bonus. <see cref="Times2"/> doubles the number sum
/// (applied before additive modifiers — see <see cref="Flip7Scoring"/>).
/// </summary>
public enum ModifierKind
{
    Plus2,
    Plus4,
    Plus6,
    Plus8,
    Plus10,
    Times2,
}

/// <summary>Action cards, resolved immediately when drawn.</summary>
public enum ActionKind
{
    /// <summary>Targeted player banks current points and exits the round (a forced Stay).</summary>
    Freeze,

    /// <summary>Targeted player accepts the next 3 cards, one at a time.</summary>
    FlipThree,

    /// <summary>Kept by a player; negates one future bust. One per player at a time.</summary>
    SecondChance,
}

/// <summary>
/// An immutable Flip 7 card. Exactly one of <see cref="Number"/>,
/// <see cref="Modifier"/> or <see cref="Action"/> is populated, indicated by
/// <see cref="Kind"/>. Use the factory methods to construct.
/// </summary>
public sealed record Flip7Card
{
    public required CardKind Kind { get; init; }
    public int? Number { get; init; }
    public ModifierKind? Modifier { get; init; }
    public ActionKind? Action { get; init; }

    public static Flip7Card OfNumber(int value)
    {
        if (value is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Number cards are 0–12.");
        return new Flip7Card { Kind = CardKind.Number, Number = value };
    }

    public static Flip7Card OfModifier(ModifierKind modifier) =>
        new() { Kind = CardKind.Modifier, Modifier = modifier };

    public static Flip7Card OfAction(ActionKind action) =>
        new() { Kind = CardKind.Action, Action = action };

    /// <summary>The flat points an additive modifier contributes (0 for x2 / non-modifiers).</summary>
    public int ModifierAdditive => Modifier switch
    {
        ModifierKind.Plus2 => 2,
        ModifierKind.Plus4 => 4,
        ModifierKind.Plus6 => 6,
        ModifierKind.Plus8 => 8,
        ModifierKind.Plus10 => 10,
        _ => 0,
    };

    public override string ToString() => Kind switch
    {
        CardKind.Number => Number!.Value.ToString(),
        CardKind.Modifier => Modifier == ModifierKind.Times2 ? "x2" : $"+{ModifierAdditive}",
        CardKind.Action => Action!.Value.ToString(),
        _ => "?",
    };
}
