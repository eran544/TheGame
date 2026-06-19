namespace Flip7Server.Game;

/// <summary>
/// Pure scoring for a single player's line at the end of a round.
/// Score order (rulebook): sum number cards → ×2 (if the x2 modifier is held) →
/// add additive modifiers (+2…+10) → +15 (if Flip 7 was achieved).
/// A busted line scores 0 — that is the caller's concern; this computes the
/// banked score for a <em>non-busted</em> line.
/// </summary>
public static class Flip7Scoring
{
    public const int Flip7Bonus = 15;
    public const int UniqueNumbersForFlip7 = 7;

    public static int Score(IEnumerable<int> numberCards, IEnumerable<ModifierKind> modifiers, bool achievedFlip7)
    {
        int sum = numberCards.Sum();
        var mods = modifiers as ICollection<ModifierKind> ?? modifiers.ToList();

        // x2 applies to the number sum before additive modifiers.
        if (mods.Contains(ModifierKind.Times2))
            sum *= 2;

        foreach (var m in mods)
            sum += Additive(m);

        if (achievedFlip7)
            sum += Flip7Bonus;

        return sum;
    }

    private static int Additive(ModifierKind m) => m switch
    {
        ModifierKind.Plus2 => 2,
        ModifierKind.Plus4 => 4,
        ModifierKind.Plus6 => 6,
        ModifierKind.Plus8 => 8,
        ModifierKind.Plus10 => 10,
        _ => 0,
    };
}
