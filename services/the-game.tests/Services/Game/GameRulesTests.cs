using FluentAssertions;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

public class GameRulesTests
{
    [Theory]
    [InlineData(1, false, 8)]
    [InlineData(2, false, 7)]
    [InlineData(3, false, 6)]
    [InlineData(4, false, 6)]
    [InlineData(5, false, 6)]
    [InlineData(1, true, 7)]
    [InlineData(2, true, 6)]
    [InlineData(3, true, 5)]
    [InlineData(4, true, 5)]
    [InlineData(5, true, 5)]
    public void GetInitialHandSize_ReturnsCorrectCount(int playerCount, bool expert, int expected)
    {
        GameRules.GetInitialHandSize(playerCount, expert).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void GetInitialHandSize_OutOfRange_Throws(int playerCount)
    {
        Action act = () => GameRules.GetInitialHandSize(playerCount, isExpertMode: false);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 3)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    public void GetMinCardsPerTurn_ReflectsDrawPileAndExpert(bool drawEmpty, bool expert, int expected)
    {
        GameRules.GetMinCardsPerTurn(drawEmpty, expert).Should().Be(expected);
    }
}
