namespace TheGameServer.Services.Game;

public static class GameRules
{
    public const int AscendingStartValue = 1;
    public const int DescendingStartValue = 100;
    public const int BackwardsTrickDelta = 10;

    public const int MinPlayers = 1;
    public const int MaxPlayers = 5;

    public static int GetInitialHandSize(int playerCount, bool isExpertMode)
    {
        if (playerCount < MinPlayers || playerCount > MaxPlayers)
            throw new ArgumentOutOfRangeException(
                nameof(playerCount),
                $"Player count must be between {MinPlayers} and {MaxPlayers}");

        var reduction = isExpertMode ? 1 : 0;
        return playerCount switch
        {
            1 => 8 - reduction,
            2 => 7 - reduction,
            _ => 6 - reduction
        };
    }

    public static int GetMinCardsPerTurn(bool drawPileEmpty, bool isExpertMode)
    {
        if (drawPileEmpty) return 1;
        return isExpertMode ? 3 : 2;
    }
}
