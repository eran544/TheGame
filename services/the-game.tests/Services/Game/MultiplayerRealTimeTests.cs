using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

/// <summary>
/// Integration tests for real-time state synchronisation and disconnection handling (Task 11.1).
/// These tests exercise the service layer with an in-memory database.
/// </summary>
public class MultiplayerRealTimeTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GameService _sut;
    private readonly StubShuffler _shuffler;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    public MultiplayerRealTimeTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.AddRange(
            new User { Id = _aliceId, Username = "alice", PasswordHash = "x" },
            new User { Id = _bobId, Username = "bob", PasswordHash = "x" });
        _db.PlayerStatistics.AddRange(
            new PlayerStatistics { UserId = _aliceId },
            new PlayerStatistics { UserId = _bobId });
        _db.SaveChanges();

        _shuffler = new StubShuffler();
        _sut = GameServiceFactory.Create(_db, _shuffler);
    }

    public void Dispose() => _db.Dispose();

    // ── Real-time state synchronisation ──────────────────────────────────────

    [Fact]
    public async Task AfterTurn_NonActingPlayerSeesUpdatedPileState()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        // Alice plays the first two cards in her ordered hand onto ascending piles.
        await _sut.PlayTurnAsync(sessionId, _aliceId, new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending2),
        });

        // Bob queries the state — he should see Alice's plays reflected.
        var bobState = (await _sut.GetGameStateAsync(sessionId, _bobId)).Value!;
        bobState.Piles.Ascending1.Should().Be(2);
        bobState.Piles.Ascending2.Should().Be(3);
    }

    [Fact]
    public async Task AfterTurn_CurrentPlayerAdvancesToNextPlayer_AllClientsAgree()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        var turnResult = await _sut.PlayTurnAsync(sessionId, _aliceId, new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending1),
        });

        // Both Alice's HTTP response and Bob's GET must agree on whose turn it is.
        var aliceView = turnResult.Value!.State;
        var bobView = (await _sut.GetGameStateAsync(sessionId, _bobId)).Value!;

        aliceView.CurrentPlayerId.Should().Be(_bobId);
        bobView.CurrentPlayerId.Should().Be(_bobId);
    }

    [Fact]
    public async Task AfterTurn_PlayerHandCountsReflectCardsDrawn()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        // Alice plays two cards; she should draw two replacements.
        var result = await _sut.PlayTurnAsync(sessionId, _aliceId, new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending1),
        });

        result.Success.Should().BeTrue();
        var alicePov = result.Value!.State;
        alicePov.Hand.Should().HaveCount(7); // played 2, drew 2 → still 7

        // Bob's view should show Alice now has 7 cards.
        var bobPov = (await _sut.GetGameStateAsync(sessionId, _bobId)).Value!;
        var aliceInBobView = bobPov.Players!.Single(p => p.UserId == _aliceId);
        aliceInBobView.HandCount.Should().Be(7);
    }

    [Fact]
    public async Task GetGameState_BothPlayersSeeSamePublicData_ButDifferentHands()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        var aliceView = (await _sut.GetGameStateAsync(sessionId, _aliceId)).Value!;
        var bobView = (await _sut.GetGameStateAsync(sessionId, _bobId)).Value!;

        // Shared public state must match.
        aliceView.Piles.Should().Be(bobView.Piles);
        aliceView.DrawPileCount.Should().Be(bobView.DrawPileCount);
        aliceView.CurrentPlayerId.Should().Be(bobView.CurrentPlayerId);

        // Each player sees only their own hand.
        aliceView.Hand.Should().NotBeEmpty();
        bobView.Hand.Should().NotBeEmpty();
        aliceView.Hand.Should().NotBeEquivalentTo(bobView.Hand);
    }

    // ── LeaveResult return values ─────────────────────────────────────────────

    [Fact]
    public async Task LeaveGame_DuringActiveGame_ReturnsGameEndedTrue()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        var result = await _sut.LeaveGameAsync(sessionId, _aliceId);

        result.Success.Should().BeTrue();
        result.Value!.GameEnded.Should().BeTrue();
    }

    [Fact]
    public async Task LeaveGame_DuringLobby_ReturnsGameEndedFalse()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        var result = await _sut.LeaveGameAsync(lobby.SessionId, _bobId);

        result.Success.Should().BeTrue();
        result.Value!.GameEnded.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveGame_WhenGameAlreadyEnded_IsIdempotentAndReturnsGameEndedFalse()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        // First leave ends the game.
        await _sut.LeaveGameAsync(sessionId, _aliceId);

        // Second call (simulates hub OnDisconnectedAsync firing after HTTP leave).
        var secondResult = await _sut.LeaveGameAsync(sessionId, _aliceId);

        secondResult.Success.Should().BeTrue();
        secondResult.Value!.GameEnded.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveGame_DuringActiveGame_RecordsDisconnectionResult()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        await _sut.LeaveGameAsync(sessionId, _aliceId);

        var gameResult = await _db.GameResults.SingleAsync(r => r.GameSessionId == sessionId);
        gameResult.EndReason.Should().Be("disconnection");
        gameResult.IsPerfectGame.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveGame_CalledTwiceForSameActiveGame_DoesNotCreateDuplicateResult()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        await _sut.LeaveGameAsync(sessionId, _aliceId);
        await _sut.LeaveGameAsync(sessionId, _aliceId); // second call must not throw or duplicate

        var resultCount = await _db.GameResults.CountAsync(r => r.GameSessionId == sessionId);
        resultCount.Should().Be(1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
