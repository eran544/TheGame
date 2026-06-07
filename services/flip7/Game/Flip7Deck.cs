namespace Flip7Server.Game;

/// <summary>
/// The 94-card Flip 7 deck. Composition (reconciled to exactly 94 cards):
/// <list type="bullet">
///   <item>Number cards (79): value 0 has 1 copy; values 1–12 have <c>value</c>
///   copies (1→1, 2→2, … 12→12). Sum = 1 + (1+2+…+12) = 79.</item>
///   <item>Modifier cards (6): one each of +2, +4, +6, +8, +10, x2.</item>
///   <item>Action cards (9): Freeze ×3, Flip Three ×3, Second Chance ×3.</item>
/// </list>
/// 79 + 6 + 9 = 94.
///
/// <para>Note: the prose rulebook is internally inconsistent — its number-card
/// header says 78 while its own table sums to 79, and its modifier table sums to
/// 11. The only composition that satisfies the stated 94-card total is the one
/// above, which also matches the published game. The deck is deliberately
/// skewed: higher numbers appear more often, raising both their value and their
/// duplicate-bust risk.</para>
/// </summary>
public static class Flip7Deck
{
    public const int TotalCards = 94;
    public const int NumberCards = 79;
    public const int ModifierCards = 6;
    public const int ActionCards = 9;
    public const int ActionCopies = 3;

    /// <summary>Builds a fresh, ordered (unshuffled) 94-card deck.</summary>
    public static List<Flip7Card> CreateOrdered()
    {
        var deck = new List<Flip7Card>(TotalCards);

        // Number cards: 0 ×1, then n ×n for 1..12.
        deck.Add(Flip7Card.OfNumber(0));
        for (int value = 1; value <= 12; value++)
            for (int copy = 0; copy < value; copy++)
                deck.Add(Flip7Card.OfNumber(value));

        // Modifier cards: one of each.
        foreach (var m in Enum.GetValues<ModifierKind>())
            deck.Add(Flip7Card.OfModifier(m));

        // Action cards: three of each.
        foreach (var a in Enum.GetValues<ActionKind>())
            for (int copy = 0; copy < ActionCopies; copy++)
                deck.Add(Flip7Card.OfAction(a));

        return deck;
    }
}
