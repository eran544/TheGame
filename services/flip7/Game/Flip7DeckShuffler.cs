namespace Flip7Server.Game;

/// <summary>Produces shuffled Flip 7 deck instances.</summary>
public interface IFlip7DeckShuffler
{
    List<Flip7Card> CreateShuffledDeck();
}

/// <summary>
/// Fisher–Yates shuffler. Accepts an injected <see cref="Random"/> so games can
/// be made deterministic in tests (seeded) while production uses the shared RNG.
/// </summary>
public sealed class Flip7DeckShuffler : IFlip7DeckShuffler
{
    private readonly Random _random;

    public Flip7DeckShuffler() : this(Random.Shared) { }

    public Flip7DeckShuffler(Random random) => _random = random;

    public List<Flip7Card> CreateShuffledDeck()
    {
        var deck = Flip7Deck.CreateOrdered();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }
}
