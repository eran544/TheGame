using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;
using TheGameServer.Services;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services;

public class StatisticsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly StatisticsService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public StatisticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.Add(new User { Id = _userId, Username = "alice", PasswordHash = "x" });
        _db.SaveChanges();

        _sut = new StatisticsService(_db, new GameEngine());
    }

    public void Dispose() => _db.Dispose();

    // ── Helper ──────────────────────────────────────────────────────────────

    private Guid SeedCompletedGame(
        int cardsRemaining,
        bool isExpertMode = false,
        string endReason = "completed",
        int? durationMinutes = 5,
        DateTime? completedAt = null,
        Guid? userId = null)
    {
        var uid = userId ?? _userId;
        var session = new GameSession
        {
            CreatedBy = uid,
            GamePhase = "ended",
            MaxPlayers = 1,
            IsExpertMode = isExpertMode,
            StartedAt = DateTime.UtcNow.AddMinutes(-(durationMinutes ?? 0)),
            EndedAt = DateTime.UtcNow
        };

        var player = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = uid,
            PlayerIndex = 0,
            IsAI = false,
            IsSpectator = false
        };

        var result = new GameResult
        {
            GameSessionId = session.Id,
            TotalCardsRemaining = cardsRemaining,
            IsPerfectGame = cardsRemaining == 0,
            EndReason = endReason,
            GameDurationMinutes = durationMinutes,
            CompletedAt = completedAt ?? DateTime.UtcNow
        };

        _db.GameSessions.Add(session);
        _db.GamePlayers.Add(player);
        _db.GameResults.Add(result);
        _db.SaveChanges();

        return result.GameSessionId;
    }

    // ── GetPlayerStatisticsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetPlayerStatistics_WhenNoRowExists_ReturnsNull()
    {
        var result = await _sut.GetPlayerStatisticsAsync(_userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPlayerStatistics_MapsAllFieldsCorrectly()
    {
        _db.PlayerStatistics.Add(new PlayerStatistics
        {
            UserId = _userId,
            TotalGames = 10,
            PerfectGames = 3,
            BestScore = 0,
            AverageRemainingCards = 4.5m,
            TotalPlayTimeMinutes = 120,
            AIAssistedGames = 1,
            LastUpdated = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();

        var dto = await _sut.GetPlayerStatisticsAsync(_userId);

        dto.Should().NotBeNull();
        dto!.TotalGames.Should().Be(10);
        dto.PerfectGames.Should().Be(3);
        dto.BestScore.Should().Be(0);
        dto.AverageRemainingCards.Should().Be(4.5m);
        dto.TotalPlayTimeMinutes.Should().Be(120);
        dto.AIAssistedGames.Should().Be(1);
        dto.LastUpdated.Should().Be(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetPlayerStatistics_WhenBestScoreIsNull_ReturnsNullBestScore()
    {
        _db.PlayerStatistics.Add(new PlayerStatistics { UserId = _userId, TotalGames = 0, BestScore = null });
        _db.SaveChanges();

        var dto = await _sut.GetPlayerStatisticsAsync(_userId);

        dto!.BestScore.Should().BeNull();
    }

    // ── GetGameHistoryAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetGameHistory_WhenNoGames_ReturnsEmpty()
    {
        var result = await _sut.GetGameHistoryAsync(_userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGameHistory_ReturnsMostRecentFirst()
    {
        var older = DateTime.UtcNow.AddDays(-2);
        var newer = DateTime.UtcNow.AddDays(-1);

        SeedCompletedGame(5, completedAt: older);
        SeedCompletedGame(3, completedAt: newer);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history.Should().HaveCount(2);
        history[0].PlayedAt.Should().BeCloseTo(newer, TimeSpan.FromSeconds(1));
        history[1].PlayedAt.Should().BeCloseTo(older, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetGameHistory_DoesNotReturnOtherPlayersGames()
    {
        var otherUserId = Guid.NewGuid();
        _db.Users.Add(new User { Id = otherUserId, Username = "bob", PasswordHash = "x" });
        _db.SaveChanges();

        SeedCompletedGame(5, userId: otherUserId);
        SeedCompletedGame(3);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history.Should().HaveCount(1);
        history[0].CardsRemaining.Should().Be(3);
    }

    [Fact]
    public async Task GetGameHistory_AssignsRating_Perfect_WhenZeroCardsRemaining()
    {
        SeedCompletedGame(0);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history[0].Rating.Should().Be("Perfect");
        history[0].IsPerfectGame.Should().BeTrue();
        history[0].CardsRemaining.Should().Be(0);
    }

    [Fact]
    public async Task GetGameHistory_AssignsRating_Excellent_WhenOneToNineRemaining()
    {
        SeedCompletedGame(7);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history[0].Rating.Should().Be("Excellent");
        history[0].IsPerfectGame.Should().BeFalse();
        history[0].CardsRemaining.Should().Be(7);
    }

    [Fact]
    public async Task GetGameHistory_AssignsRating_TryAgain_WhenTenOrMoreRemaining()
    {
        SeedCompletedGame(15);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history[0].Rating.Should().Be("TryAgain");
        history[0].IsPerfectGame.Should().BeFalse();
        history[0].CardsRemaining.Should().Be(15);
    }

    [Fact]
    public async Task GetGameHistory_MapsAllFieldsCorrectly()
    {
        var fixedTime = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        SeedCompletedGame(0, isExpertMode: true, endReason: "completed", durationMinutes: 12, completedAt: fixedTime);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history.Should().HaveCount(1);
        var item = history[0];
        item.PlayedAt.Should().BeCloseTo(fixedTime, TimeSpan.FromSeconds(1));
        item.CardsRemaining.Should().Be(0);
        item.IsPerfectGame.Should().BeTrue();
        item.DurationMinutes.Should().Be(12);
        item.EndReason.Should().Be("completed");
        item.IsExpertMode.Should().BeTrue();
    }

    [Fact]
    public async Task GetGameHistory_LimitsResultsToFifty()
    {
        for (var i = 0; i < 60; i++)
            SeedCompletedGame(i % 20);

        var history = await _sut.GetGameHistoryAsync(_userId);

        history.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetGameHistory_ExcludesAIPlayerGames()
    {
        var session = new GameSession
        {
            CreatedBy = _userId,
            GamePhase = "ended",
            MaxPlayers = 2,
            IsExpertMode = false,
            EndedAt = DateTime.UtcNow
        };

        var aiPlayer = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = _userId,
            PlayerIndex = 0,
            IsAI = true,
            IsSpectator = false
        };

        var result = new GameResult
        {
            GameSessionId = session.Id,
            TotalCardsRemaining = 5,
            IsPerfectGame = false,
            EndReason = "completed",
            CompletedAt = DateTime.UtcNow
        };

        _db.GameSessions.Add(session);
        _db.GamePlayers.Add(aiPlayer);
        _db.GameResults.Add(result);
        _db.SaveChanges();

        var history = await _sut.GetGameHistoryAsync(_userId);

        history.Should().BeEmpty();
    }
}
