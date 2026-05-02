using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

public class GameServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GameService _sut;
    private readonly StubShuffler _shuffler;
    private readonly Guid _userId = Guid.NewGuid();

    public GameServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.Add(new User { Id = _userId, Username = "alice", PasswordHash = "x" });
        _db.PlayerStatistics.Add(new PlayerStatistics { UserId = _userId });
        _db.SaveChanges();

        _shuffler = new StubShuffler();
        _sut = new GameService(_db, new GameEngine(), _shuffler);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task StartSinglePlayerGame_DealsEightCardsAndCreates90CardDrawPile()
    {
        var result = await _sut.StartSinglePlayerGameAsync(_userId);

        result.Success.Should().BeTrue();
        var view = result.Value!;
        view.GamePhase.Should().Be("playing");
        view.IsExpertMode.Should().BeFalse();
        view.Hand.Should().HaveCount(8);
        view.DrawPileCount.Should().Be(90);
        view.PlayedCardsCount.Should().Be(0);
        view.Piles.Should().Be(PileTops.Initial());
        view.MinCardsThisTurn.Should().Be(1); // single-player always plays one card at a time

        // Persistence
        (await _db.GameSessions.CountAsync()).Should().Be(1);
        (await _db.GameStates.CountAsync()).Should().Be(1);
        (await _db.PlayerHands.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task StartSinglePlayerGame_ExpertMode_DealsSevenCards()
    {
        var result = await _sut.StartSinglePlayerGameAsync(_userId, isExpertMode: true);

        result.Success.Should().BeTrue();
        result.Value!.Hand.Should().HaveCount(7);
        result.Value.IsExpertMode.Should().BeTrue();
        result.Value.MinCardsThisTurn.Should().Be(1); // single-player always plays one card at a time
    }

    [Fact]
    public async Task StartSinglePlayerGame_UnknownUser_Fails()
    {
        var result = await _sut.StartSinglePlayerGameAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("User not found");
    }

    [Fact]
    public async Task PlayTurn_ValidPlays_UpdatesPilesAndRefillsHand()
    {
        // Hand will be cards 2..9 (first 8 of ordered deck), draw pile = 10..99
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var start = await _sut.StartSinglePlayerGameAsync(_userId);
        var sessionId = start.Value!.SessionId;

        var plays = new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending1)
        };

        var result = await _sut.PlayTurnAsync(sessionId, _userId, plays);

        result.Success.Should().BeTrue();
        var state = result.Value!.State;
        state.Piles.Ascending1.Should().Be(3);
        state.Hand.Should().HaveCount(8); // refilled
        state.Hand.Should().NotContain(new[] { 2, 3 });
        state.PlayedCardsCount.Should().Be(2);
        state.DrawPileCount.Should().Be(88);
        result.Value.GameEnded.Should().BeFalse();
    }

    [Fact]
    public async Task PlayTurn_InvalidMove_DoesNotMutateState()
    {
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var start = await _sut.StartSinglePlayerGameAsync(_userId);
        var sessionId = start.Value!.SessionId;

        // 3 then 2 on the same ascending pile is invalid (2 < 3)
        var plays = new List<CardPlay>
        {
            new(3, PileSlot.Ascending1),
            new(2, PileSlot.Ascending1)
        };

        var result = await _sut.PlayTurnAsync(sessionId, _userId, plays);

        result.Success.Should().BeFalse();

        var view = (await _sut.GetGameStateAsync(sessionId, _userId)).Value!;
        view.Piles.Should().Be(PileTops.Initial());
        view.PlayedCardsCount.Should().Be(0);
        view.Hand.Should().HaveCount(8);
        view.DrawPileCount.Should().Be(90);
    }

    [Fact]
    public async Task PlayTurn_PerfectGame_RecordsResultAndUpdatesStatistics()
    {
        // Arrange a deck where the player plays everything in order:
        // hand starts as [2..9], drawPile is 10..99 dealt one-by-one as we play
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var start = await _sut.StartSinglePlayerGameAsync(_userId);
        var sessionId = start.Value!.SessionId;

        // Drain by repeatedly playing the two lowest cards from hand onto Ascending1
        for (var iteration = 0; iteration < 49; iteration++)
        {
            var view = (await _sut.GetGameStateAsync(sessionId, _userId)).Value!;
            if (view.GamePhase == "ended") break;

            var sortedHand = view.Hand.OrderBy(x => x).ToList();
            var min = view.MinCardsThisTurn;
            var plays = sortedHand
                .Where(c => c > view.Piles.Ascending1)
                .Take(Math.Max(min, 2))
                .Select(c => new CardPlay(c, PileSlot.Ascending1))
                .ToList();

            var turn = await _sut.PlayTurnAsync(sessionId, _userId, plays);
            turn.Success.Should().BeTrue($"iteration {iteration} should succeed; error: {turn.Error}");
            if (turn.Value!.GameEnded) break;
        }

        var final = (await _sut.GetGameStateAsync(sessionId, _userId)).Value!;
        final.GamePhase.Should().Be("ended");
        final.Hand.Should().BeEmpty();
        final.DrawPileCount.Should().Be(0);
        final.FinalScore!.IsPerfectGame.Should().BeTrue();
        final.FinalScore.CardsRemaining.Should().Be(0);

        var stats = await _db.PlayerStatistics.SingleAsync(s => s.UserId == _userId);
        stats.TotalGames.Should().Be(1);
        stats.PerfectGames.Should().Be(1);
        stats.BestScore.Should().Be(0);

        var gameResult = await _db.GameResults.SingleAsync();
        gameResult.IsPerfectGame.Should().BeTrue();
        gameResult.TotalCardsRemaining.Should().Be(0);
    }

    [Fact]
    public async Task PlayTurn_OnEndedSession_Fails()
    {
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var start = await _sut.StartSinglePlayerGameAsync(_userId);
        var sessionId = start.Value!.SessionId;

        // Manually mark the session as ended
        var session = await _db.GameSessions.SingleAsync(s => s.Id == sessionId);
        session.GamePhase = "ended";
        await _db.SaveChangesAsync();

        var plays = new List<CardPlay>
        {
            new(2, PileSlot.Ascending1),
            new(3, PileSlot.Ascending1)
        };

        var result = await _sut.PlayTurnAsync(sessionId, _userId, plays);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not in progress");
    }

    [Fact]
    public async Task GetGameState_UnknownSession_Fails()
    {
        var result = await _sut.GetGameStateAsync(Guid.NewGuid(), _userId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetGameState_WhenUserIsNotPlayer_Fails()
    {
        _shuffler.SetDeck(CardDeck.CreateOrderedDeck());
        var start = await _sut.StartSinglePlayerGameAsync(_userId);

        var otherUserId = Guid.NewGuid();
        _db.Users.Add(new User { Id = otherUserId, Username = "bob", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        var result = await _sut.GetGameStateAsync(start.Value!.SessionId, otherUserId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not part of this game");
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
