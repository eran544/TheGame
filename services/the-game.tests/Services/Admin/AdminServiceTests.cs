using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using GameCommon.Auth;
using TheGameServer.Data;
using TheGameServer.DTOs.Admin;
using TheGameServer.Models;
using TheGameServer.Services;

namespace TheGameServer.Tests.Services.Admin;

public class AdminServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPasswordValidator> _passwordValidator;
    private readonly AdminService _sut;

    // Seed users
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public AdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.AddRange(
            new User { Id = _aliceId, Username = "alice", PasswordHash = "x", IsAdmin = false },
            new User { Id = _bobId, Username = "bob", PasswordHash = "x", IsAdmin = false },
            new User { Id = _adminId, Username = "admin", PasswordHash = "x", IsAdmin = true });

        _db.PlayerStatistics.AddRange(
            new PlayerStatistics { UserId = _aliceId, TotalGames = 5, PerfectGames = 1 },
            new PlayerStatistics { UserId = _bobId, TotalGames = 3, PerfectGames = 0 },
            new PlayerStatistics { UserId = _adminId });

        _db.SaveChanges();

        _passwordValidator = new Mock<IPasswordValidator>();
        _passwordValidator.Setup(v => v.Validate(It.IsAny<string>()))
            .Returns(new PasswordValidationResult(true, null));

        _sut = new AdminService(_db, _passwordValidator.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helper ────────────────────────────────────────────────────────────

    private Guid SeedPlayingSession(int extraPlayers = 0)
    {
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            CreatedBy = _aliceId,
            GamePhase = "playing",
            MaxPlayers = 4,
            StartedAt = DateTime.UtcNow,
        };
        session.Players.Add(new GamePlayer { UserId = _aliceId, GameSessionId = session.Id, PlayerIndex = 0, User = _db.Users.Find(_aliceId)! });
        session.Players.Add(new GamePlayer { UserId = _bobId, GameSessionId = session.Id, PlayerIndex = 1, User = _db.Users.Find(_bobId)! });
        for (var i = 0; i < extraPlayers; i++)
        {
            var uid = Guid.NewGuid();
            _db.Users.Add(new User { Id = uid, Username = $"player{i}", PasswordHash = "x" });
            session.Players.Add(new GamePlayer { UserId = uid, GameSessionId = session.Id, PlayerIndex = 2 + i });
        }
        _db.GameSessions.Add(session);
        _db.SaveChanges();
        return session.Id;
    }

    // ── GetDashboardStatsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_CountsUsers()
    {
        var result = await _sut.GetDashboardStatsAsync();
        result.TotalUsers.Should().Be(3); // alice, bob, admin
    }

    [Fact]
    public async Task GetDashboard_CountsActiveGames()
    {
        SeedPlayingSession();
        _db.GameSessions.Add(new GameSession { CreatedBy = _aliceId, GamePhase = "lobby", MaxPlayers = 2 });
        _db.GameSessions.Add(new GameSession { CreatedBy = _aliceId, GamePhase = "ended", MaxPlayers = 2 });
        _db.SaveChanges();

        var result = await _sut.GetDashboardStatsAsync();
        result.ActiveGames.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboard_CountsCompletedGames()
    {
        _db.GameSessions.Add(new GameSession { CreatedBy = _aliceId, GamePhase = "ended", MaxPlayers = 2 });
        _db.GameSessions.Add(new GameSession { CreatedBy = _aliceId, GamePhase = "ended", MaxPlayers = 2 });
        _db.SaveChanges();

        var result = await _sut.GetDashboardStatsAsync();
        result.TotalCompletedGames.Should().Be(2);
    }

    [Fact]
    public async Task GetDashboard_CountsChatViolations()
    {
        _db.ChatMessages.AddRange(
            new ChatMessage { GameSessionId = SeedPlayingSession(), UserId = _aliceId, Message = "ok", IsValidated = true },
            new ChatMessage { GameSessionId = SeedPlayingSession(), UserId = _bobId, Message = "bad", IsValidated = false },
            new ChatMessage { GameSessionId = SeedPlayingSession(), UserId = _bobId, Message = "also bad", IsValidated = false });
        _db.SaveChanges();

        var result = await _sut.GetDashboardStatsAsync();
        result.TotalChatViolations.Should().Be(2);
    }

    // ── GetAllUsersAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllUsers_ReturnsAllUsersAlphabetically()
    {
        var result = await _sut.GetAllUsersAsync();

        result.Should().HaveCount(3);
        result.Select(u => u.Username).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetAllUsers_PopulatesStatsFromPlayerStatistics()
    {
        var result = await _sut.GetAllUsersAsync();

        var alice = result.Single(u => u.Username == "alice");
        alice.TotalGames.Should().Be(5);
        alice.PerfectGames.Should().Be(1);
    }

    [Fact]
    public async Task GetAllUsers_MarksAdminFlag()
    {
        var result = await _sut.GetAllUsersAsync();

        result.Single(u => u.Username == "admin").IsAdmin.Should().BeTrue();
        result.Single(u => u.Username == "alice").IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllUsers_ZeroStatsWhenNoStatisticsRow()
    {
        var uid = Guid.NewGuid();
        _db.Users.Add(new User { Id = uid, Username = "nostat", PasswordHash = "x" });
        _db.SaveChanges();

        var result = await _sut.GetAllUsersAsync();

        result.Single(u => u.Username == "nostat").TotalGames.Should().Be(0);
    }

    // ── CreateUserAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_Success_PersistsUserAndStatistics()
    {
        var result = await _sut.CreateUserAsync(new AdminCreateUserRequest("newuser", "Password1", false));

        result.Success.Should().BeTrue();
        _db.Users.Should().ContainSingle(u => u.Username == "newuser");
        _db.PlayerStatistics.Should().ContainSingle(s => s.User.Username == "newuser");
    }

    [Fact]
    public async Task CreateUser_CanCreateAdminUser()
    {
        await _sut.CreateUserAsync(new AdminCreateUserRequest("newadmin", "Password1", true));

        _db.Users.Single(u => u.Username == "newadmin").IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsError()
    {
        var result = await _sut.CreateUserAsync(new AdminCreateUserRequest("alice", "Password1", false));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("taken");
    }

    [Fact]
    public async Task CreateUser_InvalidPassword_ReturnsValidatorError()
    {
        _passwordValidator.Setup(v => v.Validate("weak"))
            .Returns(new PasswordValidationResult(false, "Too weak"));

        var result = await _sut.CreateUserAsync(new AdminCreateUserRequest("fresh", "weak", false));

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Too weak");
    }

    // ── DeleteUserAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_Success_RemovesUserFromDb()
    {
        var result = await _sut.DeleteUserAsync(_aliceId);

        result.Success.Should().BeTrue();
        _db.Users.Any(u => u.Id == _aliceId).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_NotFound_ReturnsError()
    {
        var result = await _sut.DeleteUserAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteUser_AdminAccount_ReturnsError()
    {
        var result = await _sut.DeleteUserAsync(_adminId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("admin");
    }

    // ── ResetPasswordAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_Success_UpdatesPasswordHash()
    {
        var before = _db.Users.Find(_aliceId)!.PasswordHash;

        var result = await _sut.ResetPasswordAsync(_aliceId, "NewPass1");

        result.Success.Should().BeTrue();
        _db.Users.Find(_aliceId)!.PasswordHash.Should().NotBe(before);
    }

    [Fact]
    public async Task ResetPassword_NotFound_ReturnsError()
    {
        var result = await _sut.ResetPasswordAsync(Guid.NewGuid(), "NewPass1");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ResetPassword_InvalidPassword_ReturnsError()
    {
        _passwordValidator.Setup(v => v.Validate("bad"))
            .Returns(new PasswordValidationResult(false, "Too short"));

        var result = await _sut.ResetPasswordAsync(_aliceId, "bad");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Too short");
    }

    // ── GetActiveGamesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetActiveGames_ReturnsOnlyPlayingSessions()
    {
        SeedPlayingSession();
        _db.GameSessions.Add(new GameSession { CreatedBy = _aliceId, GamePhase = "lobby", MaxPlayers = 2 });
        _db.GameSessions.Add(new GameSession { CreatedBy = _aliceId, GamePhase = "ended", MaxPlayers = 2 });
        _db.SaveChanges();

        var result = await _sut.GetActiveGamesAsync();

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveGames_PopulatesHostUsername()
    {
        SeedPlayingSession();

        var result = await _sut.GetActiveGamesAsync();

        result[0].HostUsername.Should().Be("alice");
    }

    [Fact]
    public async Task GetActiveGames_ExcludesSpectatorsFromPlayerCount()
    {
        var sessionId = SeedPlayingSession();
        var specId = Guid.NewGuid();
        _db.Users.Add(new User { Id = specId, Username = "spectator", PasswordHash = "x" });
        _db.GamePlayers.Add(new GamePlayer { UserId = specId, GameSessionId = sessionId, PlayerIndex = 2, IsSpectator = true });
        _db.SaveChanges();

        var result = await _sut.GetActiveGamesAsync();

        result[0].PlayerCount.Should().Be(2); // alice + bob, not spectator
    }

    [Fact]
    public async Task GetActiveGames_IncludesPlayerListWithUsernames()
    {
        SeedPlayingSession();

        var result = await _sut.GetActiveGamesAsync();

        result[0].Players.Select(p => p.Username).Should().BeEquivalentTo(new[] { "alice", "bob" });
    }

    [Fact]
    public async Task GetActiveGames_ReturnsEmptyWhenNoActiveSessions()
    {
        var result = await _sut.GetActiveGamesAsync();
        result.Should().BeEmpty();
    }

    // ── ForceEndGameAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ForceEndGame_Success_SetsPhaseToEnded()
    {
        var sessionId = SeedPlayingSession();

        var result = await _sut.ForceEndGameAsync(sessionId);

        result.Success.Should().BeTrue();
        _db.GameSessions.Find(sessionId)!.GamePhase.Should().Be("ended");
    }

    [Fact]
    public async Task ForceEndGame_Success_CreatesGameResultWithAdminEndReason()
    {
        var sessionId = SeedPlayingSession();

        await _sut.ForceEndGameAsync(sessionId);

        var gameResult = await _db.GameResults.SingleAsync(r => r.GameSessionId == sessionId);
        gameResult.EndReason.Should().Be("admin_ended");
    }

    [Fact]
    public async Task ForceEndGame_NotFound_ReturnsError()
    {
        var result = await _sut.ForceEndGameAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ForceEndGame_AlreadyEnded_ReturnsError()
    {
        var session = new GameSession { CreatedBy = _aliceId, GamePhase = "ended", MaxPlayers = 2 };
        _db.GameSessions.Add(session);
        _db.SaveChanges();

        var result = await _sut.ForceEndGameAsync(session.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already ended");
    }

    // ── KickPlayerAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task KickPlayer_Success_SetsDisconnectedAt()
    {
        var sessionId = SeedPlayingSession();

        var result = await _sut.KickPlayerAsync(sessionId, _aliceId);

        result.Success.Should().BeTrue();
        var player = await _db.GamePlayers.FirstAsync(p => p.GameSessionId == sessionId && p.UserId == _aliceId);
        player.DisconnectedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task KickPlayer_PlayerNotInSession_ReturnsError()
    {
        var sessionId = SeedPlayingSession();

        var result = await _sut.KickPlayerAsync(sessionId, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task KickPlayer_SessionEnded_ReturnsError()
    {
        var sessionId = SeedPlayingSession();
        var session = _db.GameSessions.Find(sessionId)!;
        session.GamePhase = "ended";
        _db.SaveChanges();

        var result = await _sut.KickPlayerAsync(sessionId, _aliceId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not active");
    }

    [Fact]
    public async Task KickPlayer_CannotKickAIPlayer()
    {
        var sessionId = SeedPlayingSession();
        var aiId = Guid.NewGuid();
        _db.Users.Add(new User { Id = aiId, Username = "AI_1", PasswordHash = "x" });
        _db.GamePlayers.Add(new GamePlayer { UserId = aiId, GameSessionId = sessionId, PlayerIndex = 2, IsAI = true });
        _db.SaveChanges();

        var result = await _sut.KickPlayerAsync(sessionId, aiId);

        result.Success.Should().BeFalse();
    }
}
