namespace Flip7Server.DTOs;

/// <summary>Request to create a solo (single-player) game.</summary>
public record CreateSoloRequest(int? TargetScore);

/// <summary>One AI opponent's lobby configuration (style/difficulty chosen per AI player).</summary>
public record Flip7AiSpec
{
    public string? Username { get; init; }
    public string Style { get; init; } = "balanced";
    public string Difficulty { get; init; } = "medium";
}

/// <summary>Request to create a vs-AI or online game with AI opponents.</summary>
public record CreateGameRequest
{
    public int? TargetScore { get; init; }
    public IReadOnlyList<Flip7AiSpec> AiPlayers { get; init; } = new List<Flip7AiSpec>();
}

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

    /// <summary>The duplicate number that busted this line; null unless busted.</summary>
    public int? BustedNumber { get; init; }

    /// <summary>The line's current banked value (0 if busted).</summary>
    public required int RoundScore { get; init; }
}

/// <summary>Request to resolve a pending action card's target.</summary>
public record ChooseTargetRequest(Guid TargetPlayerId);

/// <summary>
/// One resolution step from the last action (card reveals, busts with the
/// duplicate card, freezes, …) so clients can narrate and animate precisely.
/// </summary>
public record Flip7EventDto
{
    public required string Type { get; init; }
    public required Guid PlayerId { get; init; }
    public Guid? SourcePlayerId { get; init; }

    /// <summary>Compact card label when relevant: "7", "+10", "x2", "Freeze", "FlipThree".</summary>
    public string? Card { get; init; }

    public string? Detail { get; init; }
}

/// <summary>An action card waiting for its drawer to choose a target.</summary>
public record Flip7PendingActionDto
{
    /// <summary>"Freeze" | "FlipThree".</summary>
    public required string Action { get; init; }

    /// <summary>The player (Flip7Player.Id) who drew the card and must choose.</summary>
    public required Guid DrawerId { get; init; }

    /// <summary>Players that may be targeted; everyone else is shown disabled.</summary>
    public required IReadOnlyList<Guid> CandidateIds { get; init; }
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

    /// <summary>Set while an action card waits for its drawer to pick a target; play is suspended.</summary>
    public Flip7PendingActionDto? PendingAction { get; init; }

    /// <summary>What happened during the action that produced this state (empty on plain reads).</summary>
    public IReadOnlyList<Flip7EventDto> Events { get; init; } = Array.Empty<Flip7EventDto>();

    /// <summary>Unique per state-changing action; lets clients de-duplicate Events on replays/reconnects.</summary>
    public Guid? ActionId { get; init; }
}
