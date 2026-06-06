namespace TheGameServer.Services.Game;

public interface IDeckShuffler
{
    List<int> Shuffle();
}

public class DeckShuffler : IDeckShuffler
{
    public List<int> Shuffle() => CardDeck.CreateShuffledDeck();
}
