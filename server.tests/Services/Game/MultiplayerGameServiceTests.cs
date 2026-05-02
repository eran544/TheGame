using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

public class MultiplayerGameServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GameService _sut;
    private readonly StubShuffler _shuffler;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    public MultiplayerGameServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
        _sut = new GameService(_db, new GameEngine(), _shuffler);
    }

    public void Dispose() => _db.Dispose();

    // ── Lobby creation ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMultiplayerGame_ValidPlayerCount_CreatesLobby()
    {
        var result = await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 3);

        result.Success.Should().BeTrue();
        var lobby = result.Value!;
        lobby.GamePhase.Should().Be("lobby");
        lobby.MaxPlayers.Should().Be(3);
        lobby.Players.Should().HaveCount(1);
        lobby.Players[0].UserId.Should().Be(_aliceId);
        lobby.CreatedBy.Should().Be(_aliceId);
        lobby.CanStart.Should().BeFalse(); // need ≥2 players
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public async Task CreateMultiplayerGame_InvalidPlayerCount_Fails(int count)
    {
        var result = await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: count);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── Joining ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGame_NewPlayer_JoinsLobby()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;

        var result = await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        result.Success.Should().BeTrue();
        var updated = result.Value!;
        updated.Players.Should().HaveCount(2);
        updated.CanStart.Should().BeTrue();
    }

    [Fact]
    public async Task JoinGame_AlreadyInGame_Fails()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 3)).Value!;

        var result = await _sut.JoinGameAsync(lobby.SessionId, _aliceId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Already in");
    }

    [Fact]
    public async Task JoinGame_GameFull_Fails()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        var carolId = Guid.NewGuid();
        _db.Users.Add(new User { Id = carolId, Username = "carol", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        var result = await _sut.JoinGameAsync(lobby.SessionId, carolId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("full");
    }

    [Fact]
    public async Task JoinGame_GameAlreadyStarted_Fails()
    {
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);
        await _sut.StartMultiplayerGameAsync(lobby.SessionId, _aliceId);

        var carolId = Guid.NewGuid();
        _db.Users.Add(new User { Id = carolId, Username = "carol", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        var result = await _sut.JoinGameAsync(lobby.SessionId, carolId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already started");
    }

    // ── Starting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartMultiplayerGame_ByNonCreator_Fails()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        var result = await _sut.StartMultiplayerGameAsync(lobby.SessionId, _bobId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("creator");
    }

    [Fact]
    public async Task StartMultiplayerGame_OnlyOnePlayer_Fails()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 3)).Value!;

        var result = await _sut.StartMultiplayerGameAsync(lobby.SessionId, _aliceId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("at least 2");
    }

    [Fact]
    public async Task StartMultiplayerGame_TwoPlayers_DealsHandsAndSetsFirstPlayer()
    {
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        var result = await _sut.StartMultiplayerGameAsync(lobby.SessionId, _aliceId);

        result.Success.Should().BeTrue();
        var view = result.Value!;
        view.GamePhase.Should().Be("playing");
        view.Hand.Should().HaveCount(7); // 2-player hand size
        view.CurrentPlayerId.Should().Be(_aliceId); // first player (index 0) goes first
        view.Players.Should().HaveCount(2);
        view.Players!.All(p => p.HandCount == 7).Should().BeTrue();
    }

    // ── Turn enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlayTurn_WhenNotCurrentPlayer_Fails()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        // Bob tries to play on Alice's turn
        var plays = new List<CardPlay> { new(2, PileSlot.Ascending1) };
        var result = await _sut.PlayTurnAsync(sessionId, _bobId, plays);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not your turn");
    }

    [Fact]
    public async Task PlayTurn_ByCurrentPlayer_AdvancesTurnToNextPlayer()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        // Alice plays two cards (minimum for multiplayer)
        var plays = new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending1)
        };
        var result = await _sut.PlayTurnAsync(sessionId, _aliceId, plays);

        result.Success.Should().BeTrue();
        var view = result.Value!.State;
        view.CurrentPlayerId.Should().Be(_bobId);
    }

    [Fact]
    public async Task PlayTurn_TurnWrapsAround_BackToFirstPlayer()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        // Alice plays
        await _sut.PlayTurnAsync(sessionId, _aliceId, new List<CardPlay>
        {
            new(2, PileSlot.Ascending1), new(3, PileSlot.Ascending1)
        });

        // Bob plays
        var bobView = (await _sut.GetGameStateAsync(sessionId, _bobId)).Value!;
        var bobCards = bobView.Hand.OrderBy(x => x).Take(2).ToList();
        var bobPlays = bobCards.Select(c => new CardPlay(c, PileSlot.Ascending2)).ToList();
        var result = await _sut.PlayTurnAsync(sessionId, _bobId, bobPlays);

        result.Success.Should().BeTrue();
        result.Value!.State.CurrentPlayerId.Should().Be(_aliceId);
    }

    // ── Last move metadata ───────────────────────────────────────────────────

    [Fact]
    public async Task PlayTurn_PopulatesLastMove_WithActingPlayerAndCards()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        var plays = new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending2)
        };
        var result = await _sut.PlayTurnAsync(sessionId, _aliceId, plays);

        result.Success.Should().BeTrue();
        var lastMove = result.Value!.State.LastMove;
        lastMove.Should().NotBeNull();
        lastMove!.PlayerUsername.Should().Be("alice");
        lastMove.Plays.Should().HaveCount(2);
        lastMove.Plays[0].Card.Should().Be(2);
        lastMove.Plays[0].PileSlot.Should().Be((int)PileSlot.Ascending1);
        lastMove.Plays[1].Card.Should().Be(3);
        lastMove.Plays[1].PileSlot.Should().Be((int)PileSlot.Ascending2);
    }

    // ── Leaving ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveGame_DuringLobby_RemovesPlayer()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        var result = await _sut.LeaveGameAsync(lobby.SessionId, _bobId);

        result.Success.Should().BeTrue();
        var updatedLobby = (await _sut.GetLobbyStateAsync(lobby.SessionId, _aliceId)).Value!;
        updatedLobby.Players.Should().HaveCount(1);
        updatedLobby.Players[0].UserId.Should().Be(_aliceId);
    }

    [Fact]
    public async Task LeaveGame_HostLeavesDuringLobby_TransfersHostToBob()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;
        await _sut.JoinGameAsync(lobby.SessionId, _bobId);

        await _sut.LeaveGameAsync(lobby.SessionId, _aliceId);

        var session = await _db.GameSessions.FindAsync(lobby.SessionId);
        session!.CreatedBy.Should().Be(_bobId);
    }

    [Fact]
    public async Task LeaveGame_LastPlayerInLobby_EndsSession()
    {
        var lobby = (await _sut.CreateMultiplayerGameAsync(_aliceId, maxPlayers: 2)).Value!;

        await _sut.LeaveGameAsync(lobby.SessionId, _aliceId);

        var session = await _db.GameSessions.FindAsync(lobby.SessionId);
        session!.GamePhase.Should().Be("ended");
    }

    [Fact]
    public async Task LeaveGame_DuringActiveGame_EndsSessionWithDisconnectionResult()
    {
        var sessionId = await StartTwoPlayerGameAsync();

        var result = await _sut.LeaveGameAsync(sessionId, _aliceId);

        result.Success.Should().BeTrue();
        var session = await _db.GameSessions.FindAsync(sessionId);
        session!.GamePhase.Should().Be("ended");
        var gameResult = await _db.GameResults.SingleAsync(r => r.GameSessionId == sessionId);
        gameResult.EndReason.Should().Be("disconnection");
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
