namespace Flip7Server.Game;

/// <summary>
/// Game-level (across-rounds) rules: turn-order rotation from the dealer, the
/// win threshold, and winner selection. Pure and DB-free so it can be unit
/// tested independently of persistence.
/// </summary>
public static class Flip7GameRules
{
    public const int DefaultTargetScore = 200;

    /// <summary>
    /// Builds the round's turn order from seats arranged by seat index. Play
    /// begins with the seat immediately after the dealer and proceeds around,
    /// with the dealer acting last.
    /// </summary>
    public static List<T> TurnOrderFromDealer<T>(IReadOnlyList<T> bySeat, int dealerSeat)
    {
        if (bySeat.Count == 0)
            throw new ArgumentException("Need at least one seat.", nameof(bySeat));

        int n = bySeat.Count;
        int start = ((dealerSeat % n) + n) % n;
        var order = new List<T>(n);
        for (int i = 1; i <= n; i++)
            order.Add(bySeat[(start + i) % n]);
        return order;
    }

    /// <summary>The next dealer's seat (rotates one seat each round).</summary>
    public static int NextDealerSeat(int dealerSeat, int seatCount) =>
        seatCount <= 0 ? 0 : (dealerSeat + 1) % seatCount;

    /// <summary>True once any cumulative score has reached the target (game ends after the round).</summary>
    public static bool IsGameOver(IEnumerable<int> cumulativeScores, int target) =>
        cumulativeScores.Any(s => s >= target);
}
