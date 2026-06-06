using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.DTOs.Statistics;
using TheGameServer.Services.Game;

namespace TheGameServer.Services;

public interface IStatisticsService
{
    Task<PlayerStatisticsDto?> GetPlayerStatisticsAsync(Guid userId);
    Task<List<GameHistoryItemDto>> GetGameHistoryAsync(Guid userId);
}

public class StatisticsService : IStatisticsService
{
    private readonly AppDbContext _db;
    private readonly IGameEngine _engine;

    public StatisticsService(AppDbContext db, IGameEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<PlayerStatisticsDto?> GetPlayerStatisticsAsync(Guid userId)
    {
        var stats = await _db.PlayerStatistics.SingleOrDefaultAsync(s => s.UserId == userId);
        if (stats is null) return null;

        return new PlayerStatisticsDto(
            stats.TotalGames,
            stats.PerfectGames,
            stats.BestScore,
            stats.AverageRemainingCards,
            stats.TotalPlayTimeMinutes,
            stats.AIAssistedGames,
            stats.LastUpdated);
    }

    public async Task<List<GameHistoryItemDto>> GetGameHistoryAsync(Guid userId)
    {
        var rows = await _db.GameResults
            .Include(r => r.GameSession)
            .Where(r => r.GameSession.Players.Any(p => p.UserId == userId && !p.IsAI && !p.IsSpectator))
            .OrderByDescending(r => r.CompletedAt)
            .Take(50)
            .Select(r => new
            {
                r.GameSessionId,
                r.TotalCardsRemaining,
                r.IsPerfectGame,
                r.GameDurationMinutes,
                r.EndReason,
                r.CompletedAt,
                r.GameSession.IsExpertMode
            })
            .ToListAsync();

        return rows.Select(r => new GameHistoryItemDto(
            r.GameSessionId,
            r.CompletedAt,
            r.TotalCardsRemaining,
            r.IsPerfectGame,
            _engine.CalculateScore(r.TotalCardsRemaining).Rating.ToString(),
            r.GameDurationMinutes,
            r.EndReason ?? "completed",
            r.IsExpertMode
        )).ToList();
    }
}
