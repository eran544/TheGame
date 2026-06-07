using FluentAssertions;
using Flip7Server.Game;

namespace Flip7Server.Tests.Game;

public class Flip7DeckTests
{
    [Fact]
    public void Deck_has_exactly_94_cards()
    {
        Flip7Deck.CreateOrdered().Should().HaveCount(94);
    }

    [Fact]
    public void Deck_splits_into_79_number_6_modifier_9_action()
    {
        var deck = Flip7Deck.CreateOrdered();

        deck.Count(c => c.Kind == CardKind.Number).Should().Be(79);
        deck.Count(c => c.Kind == CardKind.Modifier).Should().Be(6);
        deck.Count(c => c.Kind == CardKind.Action).Should().Be(9);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(6, 6)]
    [InlineData(11, 11)]
    [InlineData(12, 12)]
    public void Number_card_copies_equal_value_except_zero(int value, int expectedCopies)
    {
        var deck = Flip7Deck.CreateOrdered();
        deck.Count(c => c.Kind == CardKind.Number && c.Number == value).Should().Be(expectedCopies);
    }

    [Fact]
    public void Modifier_deck_has_one_of_each_kind()
    {
        var deck = Flip7Deck.CreateOrdered();
        foreach (var m in Enum.GetValues<ModifierKind>())
            deck.Count(c => c.Modifier == m).Should().Be(1, $"modifier {m} should appear once");
    }

    [Fact]
    public void Action_deck_has_three_of_each_kind()
    {
        var deck = Flip7Deck.CreateOrdered();
        foreach (var a in Enum.GetValues<ActionKind>())
            deck.Count(c => c.Action == a).Should().Be(3, $"action {a} should appear three times");
    }

    [Fact]
    public void Shuffle_preserves_the_multiset_of_cards()
    {
        var shuffler = new Flip7DeckShuffler(new Random(12345));
        var shuffled = shuffler.CreateShuffledDeck();

        shuffled.Should().HaveCount(94);
        // Same composition, regardless of order.
        shuffled.OrderBy(Key).Should().Equal(Flip7Deck.CreateOrdered().OrderBy(Key));
    }

    [Fact]
    public void Shuffle_changes_order()
    {
        var shuffled = new Flip7DeckShuffler(new Random(1)).CreateShuffledDeck();
        shuffled.Should().NotEqual(Flip7Deck.CreateOrdered());
    }

    [Fact]
    public void Number_card_rejects_out_of_range_values()
    {
        Action low = () => Flip7Card.OfNumber(-1);
        Action high = () => Flip7Card.OfNumber(13);
        low.Should().Throw<ArgumentOutOfRangeException>();
        high.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static (int, int, int, int) Key(Flip7Card c) =>
        ((int)c.Kind, c.Number ?? -1, (int)(c.Modifier ?? (ModifierKind)(-1)), (int)(c.Action ?? (ActionKind)(-1)));
}
