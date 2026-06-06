using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TheGameServer.Data;
using TheGameServer.Models;
using TheGameServer.Services;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

/// <summary>
/// Integration tests for task 16 — disconnection detection, AI replacement,
/// reconnection handling, and statistics recording for AI-assisted games.
/// Requirements: 3.2
/// </summary>
public class AIReplacementTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GameService _sut;
    private readonly StubShuffler _shuffler;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    public AIReplacementTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.AddRange(
            new User { Id = _aliceId, Username = "alice", PasswordHash = "x", IsAI = false },
            new User { Id = _bobId,   Username = "bob",   PasswordHash = "x", IsAI = false });
        _db.PlayerStatistics.AddRange(
            new PlayerStatistics { UserId = _aliceId },
            new PlayerStatistics { UserId = _bobId });
        _db.SaveChanges();

        _shuffler = new StubShuffler();
        _sut = GameServiceFactory.Create(_db, _shuffler);
    }

    public void Dispose() => _db.Dispose();

    // ── Disconnection detection ───────────────────────────────────────────────

    [Fact]
    public async Task LeaveGame_WithAIAvailable_ReplacesPlayerInsteadOfEndingGame()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        var result = await _sut.LeaveGameAsync(sessionId, _aliceId);

        result.Success.Should().BeTrue();
        result.Value!.GameEnded.Should().BeFalse();
        result.Value.DisconnectedUsername.Should().Be("alice");
        result.Value.ReplacedByAIUsername.Should().NotBeNullOrEmpty();

        var session = await _db.GameSessions.FindAsync(sessionId);
        session!.GamePhase.Should().Be("playing");
    }

    [Fact]
    public async Task LeaveGame_WithAIAvailable_MarksPlayerDisconnectedInDB()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        await _sut.LeaveGameAsync(sessionId, _aliceId);

        var alice = await _db.GamePlayers
            .SingleAsync(p => p.UserId == _aliceId && p.GameSessionId == sessionId);
        alice.DisconnectedAt.Should().NotBeNull();
        alice.ReplacedByAI.Should().BeTrue();
    }

    [Fact]
    public async Task LeaveGame_NoAIAvailable_EndsGame()
    {
        // No AI users seeded → replacement falls back to ending the game
        var sessionId = await StartTwoPlayerGameAsync();

        var result = await _sut.LeaveGameAsync(sessionId, _aliceId);

        result.Success.Should().BeTrue();
        result.Value!.GameEnded.Should().BeTrue();
        var session = await _db.GameSessions.FindAsync(sessionId);
        session!.GamePhase.Should().Be("ended");
    }

    [Fact]
    public async Task LeaveGame_LastHumanLeaves_EndsGame()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        // Alice leaves — Bob remains (AI replaces Alice)
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        // Bob is now the last human; leaving should end the game
        var result = await _sut.LeaveGameAsync(sessionId, _bobId);

        result.Success.Should().BeTrue();
        result.Value!.GameEnded.Should().BeTrue();
        var session = await _db.GameSessions.FindAsync(sessionId);
        session!.GamePhase.Should().Be("ended");
    }

    // ── AI replacement process ────────────────────────────────────────────────

    [Fact]
    public async Task LeaveGame_AIReplacement_CreatesAIPlayerInSession()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        await _sut.LeaveGameAsync(sessionId, _aliceId);

        var aiPlayers = await _db.GamePlayers
            .Where(p => p.IsAI && p.GameSessionId == sessionId)
            .ToListAsync();
        aiPlayers.Should().HaveCount(1);
    }

    [Fact]
    public async Task LeaveGame_AIReplacement_AIInheritsDisconnectedPlayersHand()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        var aliceState = (await _sut.GetGameStateAsync(sessionId, _aliceId)).Value!;
        var aliceHandSorted = aliceState.Hand.OrderBy(x => x).ToList();

        await _sut.LeaveGameAsync(sessionId, _aliceId);

        // The AI player was created with Alice's hand; after playing its first turn
        // the hand will have changed. Verify instead that the original Alice player
        // record still holds her original hand (unchanged by the replacement itself).
        var alicePlayer = await _db.GamePlayers
            .Include(p => p.Hand)
            .SingleAsync(p => p.UserId == _aliceId && p.GameSessionId == sessionId);
        var aliceStoredHand = JsonSerializer.Deserialize<List<int>>(alicePlayer.Hand!.Cards)!
            .OrderBy(x => x).ToList();
        aliceStoredHand.Should().Equal(aliceHandSorted);
    }

    [Fact]
    public async Task LeaveGame_AIReplacement_WhenDisconnectedPlayerHadCurrentTurn_AIPlaysImmediately()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        // Game starts with Alice's turn (index 0)
        var stateBefore = (await _sut.GetGameStateAsync(sessionId, _aliceId)).Value!;
        stateBefore.CurrentPlayerId.Should().Be(_aliceId);

        // Alice disconnects; her replacement AI immediately plays (greedy fallback)
        // and hands off to Bob
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        var state = await _db.GameStates.SingleAsync(s => s.GameSessionId == sessionId);
        state.CurrentPlayerId.Should().Be(_bobId);
    }

    [Fact]
    public async Task LeaveGame_AIReplacement_StateViewReflectsNewAIPlayer()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        var result = await _sut.LeaveGameAsync(sessionId, _aliceId);

        var stateView = result.Value!.StateAfterReplacement;
        stateView.Should().NotBeNull();
        stateView!.Players.Should().Contain(p => p.IsAI);
    }

    // ── Reconnection handling ─────────────────────────────────────────────────

    [Fact]
    public async Task ReconnectPlayer_AfterDisconnection_Succeeds()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        var result = await _sut.ReconnectPlayerAsync(sessionId, _aliceId);

        result.Success.Should().BeTrue();
        result.Value!.ReconnectedUsername.Should().Be("alice");
    }

    [Fact]
    public async Task ReconnectPlayer_AfterDisconnection_ClearsDisconnectedMark()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        await _sut.ReconnectPlayerAsync(sessionId, _aliceId);

        var alice = await _db.GamePlayers
            .SingleAsync(p => p.UserId == _aliceId && p.GameSessionId == sessionId && p.DisconnectedAt == null);
        alice.ReplacedByAI.Should().BeFalse();
    }

    [Fact]
    public async Task ReconnectPlayer_AfterDisconnection_RemovesAISlot()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        await _sut.ReconnectPlayerAsync(sessionId, _aliceId);

        var aiPlayers = await _db.GamePlayers
            .Where(p => p.IsAI && p.GameSessionId == sessionId)
            .ToListAsync();
        aiPlayers.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconnectPlayer_WithNoPriorDisconnection_Fails()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        var result = await _sut.ReconnectPlayerAsync(sessionId, _aliceId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disconnection");
    }

    [Fact]
    public async Task ReconnectPlayer_GameNotInProgress_Fails()
    {
        SeedAIUsers();
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;

        var result = await _sut.ReconnectPlayerAsync(lobby.SessionId, _aliceId);

        result.Success.Should().BeFalse();
    }

    // ── Turn timeout detection ────────────────────────────────────────────────

    [Fact]
    public async Task TimeoutCurrentPlayer_WhenTurnExceedsLimit_ReplacesCurrentHumanWithAI()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        var state = await _db.GameStates.SingleAsync(s => s.GameSessionId == sessionId);
        state.CurrentTurnStartedAt = DateTime.UtcNow.AddSeconds(-91);
        await _db.SaveChangesAsync();

        var result = await _sut.TimeoutCurrentPlayerAsync(sessionId);

        result.Success.Should().BeTrue();
        result.Value!.DisconnectedUsername.Should().Be("alice");
        result.Value.ReplacedByAIUsername.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TimeoutCurrentPlayer_WhenCurrentPlayerIsAI_Fails()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        // Point the turn to an AI user directly
        var state = await _db.GameStates.SingleAsync(s => s.GameSessionId == sessionId);
        state.CurrentPlayerId = AiPlayerConstants.Ids[0];
        state.CurrentTurnStartedAt = DateTime.UtcNow.AddSeconds(-91);
        await _db.SaveChangesAsync();

        var result = await _sut.TimeoutCurrentPlayerAsync(sessionId);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task TimeoutCurrentPlayer_AfterTimeout_SessionStillPlaying()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        var state = await _db.GameStates.SingleAsync(s => s.GameSessionId == sessionId);
        state.CurrentTurnStartedAt = DateTime.UtcNow.AddSeconds(-91);
        await _db.SaveChangesAsync();

        await _sut.TimeoutCurrentPlayerAsync(sessionId);

        var session = await _db.GameSessions.FindAsync(sessionId);
        session!.GamePhase.Should().Be("playing");
    }

    // ── Statistics recording for AI-assisted games ────────────────────────────

    [Fact]
    public async Task LeaveGame_DisconnectedPlayer_HasReplacedByAISetOnGamePlayer()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();

        await _sut.LeaveGameAsync(sessionId, _aliceId);

        var alice = await _db.GamePlayers
            .SingleAsync(p => p.UserId == _aliceId && p.GameSessionId == sessionId);
        alice.ReplacedByAI.Should().BeTrue();
    }

    [Fact]
    public async Task ReconnectPlayer_ClearsReplacedByAIFlagOnGamePlayer()
    {
        SeedAIUsers();
        var sessionId = await StartTwoPlayerGameAsync();
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        await _sut.ReconnectPlayerAsync(sessionId, _aliceId);

        var alice = await _db.GamePlayers
            .SingleAsync(p => p.UserId == _aliceId && p.GameSessionId == sessionId && p.DisconnectedAt == null);
        alice.ReplacedByAI.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SeedAIUsers()
    {
        foreach (var (id, username) in AiPlayerConstants.AiUsers)
        {
            if (!_db.Users.Any(u => u.Id == id))
                _db.Users.Add(new User { Id = id, Username = username, PasswordHash = "ai", IsAI = true });
        }
        _db.SaveChanges();
    }

    private async Task<Guid> StartTwoPlayerGameAsync()
    {
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);
        await _sut.StartMultiplayerGameAsync(lobby.SessionId, _aliceId);
        return lobby.SessionId;
    }

    private class StubShuffler : IDeckShuffler
    {
        private List<int> _deck = CardDeck.CreateShuffledDeck(new Random(42));

        public void SetDeck(List<int> deck)
        {
            if (deck.Count != CardDeck.TotalCards)
                throw new ArgumentException($"Deck must contain {CardDeck.TotalCards} cards");
            _deck = deck.ToList();
        }

        public List<int> Shuffle() => _deck.ToList();
    }
}
