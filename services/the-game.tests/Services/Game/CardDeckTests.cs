using FluentAssertions;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

public class CardDeckTests
{
    [Fact]
    public void CreateOrderedDeck_Contains98CardsFrom2To99()
    {
        var deck = CardDeck.CreateOrderedDeck();

        deck.Should().HaveCount(98);
        deck.First().Should().Be(2);
        deck.Last().Should().Be(99);
        deck.Should().BeInAscendingOrder();
    }

    [Fact]
    public void CreateShuffledDeck_ContainsAllCardsExactlyOnce()
    {
        var deck = CardDeck.CreateShuffledDeck(new Random(42));

        deck.Should().HaveCount(98);
        deck.Distinct().Should().HaveCount(98);
        deck.Min().Should().Be(2);
        deck.Max().Should().Be(99);
    }

    [Fact]
    public void CreateShuffledDeck_ProducesDifferentOrderingFromOrdered()
    {
        // With a fixed seed, the shuffled deck should not match the ordered deck
        var ordered = CardDeck.CreateOrderedDeck();
        var shuffled = CardDeck.CreateShuffledDeck(new Random(42));

        shuffled.Should().NotEqual(ordered);
    }

    [Fact]
    public void CreateShuffledDeck_WithSameSeed_IsDeterministic()
    {
        var deck1 = CardDeck.CreateShuffledDeck(new Random(123));
        var deck2 = CardDeck.CreateShuffledDeck(new Random(123));

        deck1.Should().Equal(deck2);
    }
}
