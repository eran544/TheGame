namespace TheGameServer.DTOs.Statistics;

public record PlayerStatisticsDto(
    int TotalGames,
    int PerfectGames,
    int? BestScore,
    decimal AverageRemainingCards,
    int TotalPlayTimeMinutes,
    int AIAssistedGames,
    DateTime? LastUpdated);

public record GameHistoryItemDto(
    Guid SessionId,
    DateTime PlayedAt,
    int CardsRemaining,
    bool IsPerfectGame,
    string Rating,
    int? DurationMinutes,
    string EndReason,
    bool IsExpertMode);
