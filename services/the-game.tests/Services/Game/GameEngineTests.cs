using FluentAssertions;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

public class GameEngineTests
{
    private readonly GameEngine _sut = new();

    [Theory]
    [InlineData(2, 1)]
    [InlineData(50, 49)]
    [InlineData(99, 1)]
    public void ValidateMove_AscendingPile_AllowsHigherCard(int card, int top)
    {
        var result = _sut.ValidateMove(card, PileSlot.Ascending1, top);
        result.IsValid.Should().BeTrue();
        result.IsBackwardsTrick.Should().BeFalse();
    }

    [Theory]
    [InlineData(25, 30)]
    [InlineData(2, 50)]
    public void ValidateMove_AscendingPile_RejectsLowerCard(int card, int top)
    {
        var result = _sut.ValidateMove(card, PileSlot.Ascending1, top);
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateMove_AscendingPile_AllowsBackwardsTrickExactly10Less()
    {
        var result = _sut.ValidateMove(card: 37, PileSlot.Ascending1, pileTopValue: 47);
        result.IsValid.Should().BeTrue();
        result.IsBackwardsTrick.Should().BeTrue();
    }

    [Theory]
    [InlineData(36, 47)]
    [InlineData(38, 47)]
    public void ValidateMove_AscendingPile_DoesNotAllowApproximateBackwardsTrick(int card, int top)
    {
        var result = _sut.ValidateMove(card, PileSlot.Ascending1, top);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(50, 60)]
    [InlineData(2, 99)]
    public void ValidateMove_DescendingPile_AllowsLowerCard(int card, int top)
    {
        var result = _sut.ValidateMove(card, PileSlot.Descending1, top);
        result.IsValid.Should().BeTrue();
        result.IsBackwardsTrick.Should().BeFalse();
    }

    [Fact]
    public void ValidateMove_DescendingPile_AllowsBackwardsTrickExactly10Greater()
    {
        var result = _sut.ValidateMove(card: 75, PileSlot.Descending1, pileTopValue: 65);
        result.IsValid.Should().BeTrue();
        result.IsBackwardsTrick.Should().BeTrue();
    }

    [Theory]
    [InlineData(74, 65)]
    [InlineData(76, 65)]
    public void ValidateMove_DescendingPile_DoesNotAllowApproximateBackwardsTrick(int card, int top)
    {
        var result = _sut.ValidateMove(card, PileSlot.Descending1, top);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(-5)]
    public void ValidateMove_OutOfRangeCard_Rejected(int card)
    {
        var result = _sut.ValidateMove(card, PileSlot.Ascending1, 5);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("between");
    }

    [Fact]
    public void ValidateMove_AscendingFromInitialOne_AcceptsAnyValidCard()
    {
        // Initial ascending pile top is 1; any card 2-99 should be valid
        _sut.ValidateMove(2, PileSlot.Ascending1, 1).IsValid.Should().BeTrue();
        _sut.ValidateMove(99, PileSlot.Ascending1, 1).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMove_DescendingFromInitial100_AcceptsAnyValidCard()
    {
        _sut.ValidateMove(99, PileSlot.Descending1, 100).IsValid.Should().BeTrue();
        _sut.ValidateMove(2, PileSlot.Descending1, 100).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateTurn_FewerThanMinCards_Fails()
    {
        var hand = new List<int> { 5, 10 };
        var piles = PileTops.Initial();
        var plays = new List<CardPlay> { new(5, PileSlot.Ascending1) };

        var result = _sut.ValidateTurn(plays, hand, piles, minCards: 2);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("at least 2");
    }

    [Fact]
    public void ValidateTurn_CardNotInHand_Fails()
    {
        var hand = new List<int> { 5, 10 };
        var piles = PileTops.Initial();
        var plays = new List<CardPlay>
        {
            new(5, PileSlot.Ascending1),
            new(99, PileSlot.Descending1)
        };

        var result = _sut.ValidateTurn(plays, hand, piles, minCards: 2);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not in hand");
    }

    [Fact]
    public void ValidateTurn_AppliesPlaysSequentiallyAndReturnsState()
    {
        var hand = new List<int> { 5, 12, 90 };
        var piles = PileTops.Initial();
        var plays = new List<CardPlay>
        {
            new(5, PileSlot.Ascending1),
            new(12, PileSlot.Ascending1),
            new(90, PileSlot.Descending1)
        };

        var result = _sut.ValidateTurn(plays, hand, piles, minCards: 2);

        result.IsValid.Should().BeTrue();
        result.ResultingPiles.Ascending1.Should().Be(12);
        result.ResultingPiles.Descending1.Should().Be(90);
        result.ResultingHand.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTurn_SecondPlayInvalidAgainstUpdatedPile_Fails()
    {
        // Playing 5 then 4 on the same ascending pile must fail because 4 < 5 (and not -10)
        var hand = new List<int> { 5, 4 };
        var piles = PileTops.Initial();
        var plays = new List<CardPlay>
        {
            new(5, PileSlot.Ascending1),
            new(4, PileSlot.Ascending1)
        };

        var result = _sut.ValidateTurn(plays, hand, piles, minCards: 2);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CanPlayMinimumCards_ReturnsTrueWhenSequenceExists()
    {
        var hand = new List<int> { 5, 12 };
        var piles = PileTops.Initial();

        _sut.CanPlayMinimumCards(hand, piles, minCards: 2).Should().BeTrue();
    }

    [Fact]
    public void CanPlayMinimumCards_ReturnsFalseWhenNoValidPlays()
    {
        // Ascending piles at 80, descending piles at 20; hand has only mid cards
        var hand = new List<int> { 50, 60 };
        var piles = new PileTops(80, 80, 20, 20);

        _sut.CanPlayMinimumCards(hand, piles, minCards: 2).Should().BeFalse();
    }

    [Fact]
    public void CanPlayMinimumCards_FindsSequenceUsingBackwardsTrick()
    {
        // Top of ascending = 47; only valid plays are >47 or exactly 37 (-10).
        // Hand has 37 and 50, both should be playable in sequence.
        var hand = new List<int> { 37, 50 };
        var piles = new PileTops(Ascending1: 47, Ascending2: 99, Descending1: 2, Descending2: 2);

        _sut.CanPlayMinimumCards(hand, piles, minCards: 2).Should().BeTrue();
    }

    [Fact]
    public void CanPlayMinimumCards_ZeroOrEmptyHand_HandledCorrectly()
    {
        _sut.CanPlayMinimumCards(new List<int>(), PileTops.Initial(), minCards: 0).Should().BeTrue();
        _sut.CanPlayMinimumCards(new List<int>(), PileTops.Initial(), minCards: 1).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, GameRating.Perfect, true)]
    [InlineData(1, GameRating.Excellent, false)]
    [InlineData(9, GameRating.Excellent, false)]
    [InlineData(10, GameRating.TryAgain, false)]
    [InlineData(50, GameRating.TryAgain, false)]
    public void CalculateScore_ReturnsExpectedRating(int cardsRemaining, GameRating expectedRating, bool expectedPerfect)
    {
        var score = _sut.CalculateScore(cardsRemaining);

        score.CardsRemaining.Should().Be(cardsRemaining);
        score.Rating.Should().Be(expectedRating);
        score.IsPerfectGame.Should().Be(expectedPerfect);
    }

    [Fact]
    public void CalculateScore_NegativeCount_Throws()
    {
        Action act = () => _sut.CalculateScore(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
