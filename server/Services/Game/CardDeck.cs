namespace TheGameServer.Services.Game;

public static class CardDeck
{
    public const int MinCardValue = 2;
    public const int MaxCardValue = 99;
    public const int TotalCards = MaxCardValue - MinCardValue + 1;

    public static List<int> CreateOrderedDeck() =>
        Enumerable.Range(MinCardValue, TotalCards).ToList();

    public static List<int> CreateShuffledDeck(Random? random = null)
    {
        random ??= Random.Shared;
        var deck = CreateOrderedDeck();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }
}
