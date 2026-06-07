namespace Flip7Server.DTOs;

/// <summary>Request to create a solo (single-player) game.</summary>
public record CreateSoloRequest(int? TargetScore);

/// <summary>One player's view within a game state response.</summary>
public record Flip7PlayerStateDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required int Seat { get; init; }
    public required bool IsAi { get; init; }
    public string? AiStyle { get; init; }
    public string? AiDifficulty { get; init; }
    public required int CumulativeScore { get; init; }

    // Current-round line (empty between rounds / before the first deal).
    public required IReadOnlyList<int> Numbers { get; init; }
    public required IReadOnlyList<string> Modifiers { get; init; }
    public required bool HasSecondChance { get; init; }
    public required string Status { get; init; }
    public required bool AchievedFlip7 { get; init; }

    /// <summary>The line's current banked value (0 if busted).</summary>
    public required int RoundScore { get; init; }
}

/// <summary>Full game state returned to clients after every action.</summary>
public record Flip7GameStateDto
{
    public required Guid Id { get; init; }
    public required string Mode { get; init; }
    public required string Status { get; init; }
    public required int TargetScore { get; init; }
    public required int RoundNumber { get; init; }
    public required int DealerSeat { get; init; }

    /// <summary>Whose turn it is (Flip7Player.Id), or null when the round has ended / game over.</summary>
    public Guid? CurrentPlayerId { get; init; }

    public required bool RoundEnded { get; init; }
    public required string RoundEndReason { get; init; }
    public Guid? WinnerId { get; init; }

    public required IReadOnlyList<Flip7PlayerStateDto> Players { get; init; }
}
