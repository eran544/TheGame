namespace Flip7Server.Services;

/// <summary>Everything the AI needs to decide Hit vs Stay for the player on turn.</summary>
public record Flip7AiMoveRequest
{
    public required IReadOnlyList<int> MyNumbers { get; init; }
    public required IReadOnlyList<string> MyModifiers { get; init; }
    public required bool HasSecondChance { get; init; }
    public required int MyRoundScore { get; init; }
    public required int MyCumulativeScore { get; init; }
    public required int TargetScore { get; init; }

    /// <summary>Number value → copies still drawable (draw pile + discard that will reshuffle).</summary>
    public required IReadOnlyDictionary<int, int> DeckRemaining { get; init; }
    public required int DrawPileCount { get; init; }
    public required IReadOnlyList<Flip7AiOpponent> Opponents { get; init; }

    public required string Style { get; init; }
    public required string Difficulty { get; init; }
}

public record Flip7AiOpponent
{
    public required int NumberCount { get; init; }
    public required int RoundScore { get; init; }
    public required string Status { get; init; }
    public required int CumulativeScore { get; init; }
}

/// <summary>Decides an AI player's Hit/Stay move (delegates to the Python AI service).</summary>
public interface IFlip7AiClient
{
    /// <summary>Returns "hit" or "stay".</summary>
    Task<string> DecideHitOrStayAsync(Flip7AiMoveRequest request, CancellationToken ct = default);
}
