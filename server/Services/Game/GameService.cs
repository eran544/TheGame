using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;

namespace TheGameServer.Services.Game;

public interface IGameService
{
    Task<GameResultEnvelope<GameStateView>> StartSinglePlayerGameAsync(Guid userId, bool isExpertMode = false);
    Task<GameResultEnvelope<TurnOutcome>> PlayTurnAsync(Guid sessionId, Guid userId, IList<CardPlay> plays);
    Task<GameResultEnvelope<GameStateView>> GetGameStateAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<bool>> AbandonGameAsync(Guid sessionId, Guid userId);
}

public record GameResultEnvelope<T>(bool Success, T? Value, string? Error)
{
    public static GameResultEnvelope<T> Ok(T value) => new(true, value, null);
    public static GameResultEnvelope<T> Fail(string error) => new(false, default, error);
}

public record GameStateView(
    Guid SessionId,
    string GamePhase,
    bool IsExpertMode,
    PileTops Piles,
    int DrawPileCount,
    int PlayedCardsCount,
    IList<int> Hand,
    int MinCardsThisTurn,
    GameScore? FinalScore);

public record TurnOutcome(GameStateView State, bool GameEnded, string? EndReason);

public class GameService : IGameService
{
    private readonly AppDbContext _db;
    private readonly IGameEngine _engine;
    private readonly IDeckShuffler _shuffler;

    public GameService(AppDbContext db, IGameEngine engine, IDeckShuffler shuffler)
    {
        _db = db;
        _engine = engine;
        _shuffler = shuffler;
    }

    public async Task<GameResultEnvelope<GameStateView>> StartSinglePlayerGameAsync(Guid userId, bool isExpertMode = false)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return GameResultEnvelope<GameStateView>.Fail("User not found");

        var deck = _shuffler.Shuffle();
        var handSize = GameRules.GetInitialHandSize(playerCount: 1, isExpertMode);
        var hand = deck.Take(handSize).ToList();
        var drawPile = deck.Skip(handSize).ToList();

        var session = new GameSession
        {
            CreatedBy = userId,
            GamePhase = "playing",
            MaxPlayers = 1,
            IsExpertMode = isExpertMode,
            StartedAt = DateTime.UtcNow
        };

        var player = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = userId,
            PlayerIndex = 0
        };

        var state = new GameState
        {
            GameSessionId = session.Id,
            CurrentPlayerId = player.Id,
            AscendingPile1 = GameRules.AscendingStartValue,
            AscendingPile2 = GameRules.AscendingStartValue,
            DescendingPile1 = GameRules.DescendingStartValue,
            DescendingPile2 = GameRules.DescendingStartValue,
            DrawPileCards = JsonSerializer.Serialize(drawPile),
            PlayedCardsCount = 0
        };

        var playerHand = new PlayerHand
        {
            GameSessionId = session.Id,
            PlayerId = player.Id,
            Cards = JsonSerializer.Serialize(hand)
        };

        _db.GameSessions.Add(session);
        _db.GamePlayers.Add(player);
        _db.GameStates.Add(state);
        _db.PlayerHands.Add(playerHand);
        await _db.SaveChangesAsync();

        return GameResultEnvelope<GameStateView>.Ok(BuildView(session, state, hand, drawPile, finalScore: null));
    }

    public async Task<GameResultEnvelope<TurnOutcome>> PlayTurnAsync(Guid sessionId, Guid userId, IList<CardPlay> plays)
    {
        var context = await LoadGameContextAsync(sessionId, userId);
        if (context.Error is not null)
            return GameResultEnvelope<TurnOutcome>.Fail(context.Error);

        var session = context.Session!;
        var state = context.State!;
        var player = context.Player!;
        var hand = context.Hand!;
        var drawPile = context.DrawPile!;

        if (session.GamePhase != "playing")
            return GameResultEnvelope<TurnOutcome>.Fail("Game is not in progress");

        var minCards = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);

        var piles = new PileTops(state.AscendingPile1, state.AscendingPile2, state.DescendingPile1, state.DescendingPile2);
        var validation = _engine.ValidateTurn(plays, hand, piles, minCards);
        if (!validation.IsValid)
            return GameResultEnvelope<TurnOutcome>.Fail(validation.Error!);

        var newHand = validation.ResultingHand.ToList();
        var newPiles = validation.ResultingPiles;

        var refillCount = drawPile.Count == 0
            ? 0
            : Math.Min(plays.Count, drawPile.Count);

        for (var i = 0; i < refillCount; i++)
        {
            newHand.Add(drawPile[0]);
            drawPile.RemoveAt(0);
        }

        state.AscendingPile1 = newPiles.Ascending1;
        state.AscendingPile2 = newPiles.Ascending2;
        state.DescendingPile1 = newPiles.Descending1;
        state.DescendingPile2 = newPiles.Descending2;
        state.PlayedCardsCount += plays.Count;
        state.DrawPileCards = JsonSerializer.Serialize(drawPile);
        state.UpdatedAt = DateTime.UtcNow;

        player.Hand!.Cards = JsonSerializer.Serialize(newHand);
        player.Hand.UpdatedAt = DateTime.UtcNow;

        var perfectGame = newHand.Count == 0 && drawPile.Count == 0;
        var nextMinCards = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);
        var canContinue = !perfectGame && _engine.CanPlayMinimumCards(newHand, newPiles, nextMinCards);

        GameScore? finalScore = null;
        var gameEnded = perfectGame || !canContinue;
        string? endReason = null;

        if (gameEnded)
        {
            var cardsRemaining = newHand.Count + drawPile.Count;
            finalScore = _engine.CalculateScore(cardsRemaining);
            endReason = perfectGame ? "completed" : "completed";

            session.GamePhase = "ended";
            session.EndedAt = DateTime.UtcNow;

            var durationMinutes = session.StartedAt is null
                ? (int?)null
                : (int)Math.Round((session.EndedAt!.Value - session.StartedAt.Value).TotalMinutes);

            var result = new GameResult
            {
                GameSessionId = session.Id,
                TotalCardsRemaining = cardsRemaining,
                IsPerfectGame = finalScore.IsPerfectGame,
                GameDurationMinutes = durationMinutes,
                EndReason = endReason
            };
            _db.GameResults.Add(result);

            _db.PlayerGameStats.Add(new PlayerGameStat
            {
                GameResultId = result.Id,
                UserId = userId,
                CardsInHand = newHand.Count,
                PlayTimeMinutes = durationMinutes
            });

            await UpdatePlayerStatisticsAsync(userId, finalScore, durationMinutes);
        }

        await _db.SaveChangesAsync();

        var view = BuildView(session, state, newHand, drawPile, finalScore);
        return GameResultEnvelope<TurnOutcome>.Ok(new TurnOutcome(view, gameEnded, endReason));
    }

    public async Task<GameResultEnvelope<GameStateView>> GetGameStateAsync(Guid sessionId, Guid userId)
    {
        var context = await LoadGameContextAsync(sessionId, userId);
        if (context.Error is not null)
            return GameResultEnvelope<GameStateView>.Fail(context.Error);

        GameScore? finalScore = null;
        if (context.Session!.GamePhase == "ended")
        {
            var stored = await _db.GameResults.SingleOrDefaultAsync(r => r.GameSessionId == sessionId);
            if (stored is not null)
                finalScore = _engine.CalculateScore(stored.TotalCardsRemaining);
        }

        var view = BuildView(context.Session, context.State!, context.Hand!, context.DrawPile!, finalScore);
        return GameResultEnvelope<GameStateView>.Ok(view);
    }

    public async Task<GameResultEnvelope<bool>> AbandonGameAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.State)
            .Include(s => s.Players).ThenInclude(p => p.Hand)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<bool>.Fail("Game session not found");
        if (session.Players.All(p => p.UserId != userId)) return GameResultEnvelope<bool>.Fail("Player is not part of this game");
        if (session.GamePhase == "ended") return GameResultEnvelope<bool>.Fail("Game already ended");

        var player = session.Players.First(p => p.UserId == userId);
        var handList = player.Hand is not null
            ? JsonSerializer.Deserialize<List<int>>(player.Hand.Cards) ?? new List<int>()
            : new List<int>();
        var drawPile = session.State is not null
            ? JsonSerializer.Deserialize<List<int>>(session.State.DrawPileCards) ?? new List<int>()
            : new List<int>();

        session.GamePhase = "ended";
        session.EndedAt = DateTime.UtcNow;

        _db.GameResults.Add(new GameResult
        {
            GameSessionId = sessionId,
            TotalCardsRemaining = handList.Count + drawPile.Count,
            IsPerfectGame = false,
            EndReason = "abandoned"
        });

        await _db.SaveChangesAsync();
        return GameResultEnvelope<bool>.Ok(true);
    }

    private async Task<GameContext> LoadGameContextAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.State)
            .Include(s => s.Players).ThenInclude(p => p.Hand)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            return GameContext.Failed("Game session not found");

        if (session.State is null)
            return GameContext.Failed("Game state not initialized");

        var player = session.Players.SingleOrDefault(p => p.UserId == userId);
        if (player is null)
            return GameContext.Failed("Player is not part of this game");

        if (player.Hand is null)
            return GameContext.Failed("Player hand not initialized");

        var hand = JsonSerializer.Deserialize<List<int>>(player.Hand.Cards) ?? new List<int>();
        var drawPile = JsonSerializer.Deserialize<List<int>>(session.State.DrawPileCards) ?? new List<int>();

        return new GameContext(session, session.State, player, hand, drawPile, null);
    }

    private async Task UpdatePlayerStatisticsAsync(Guid userId, GameScore score, int? durationMinutes)
    {
        var stats = await _db.PlayerStatistics.SingleOrDefaultAsync(s => s.UserId == userId);
        if (stats is null)
        {
            stats = new PlayerStatistics { UserId = userId };
            _db.PlayerStatistics.Add(stats);
        }

        var previousTotal = stats.TotalGames;
        var previousAvg = stats.AverageRemainingCards;

        stats.TotalGames = previousTotal + 1;
        if (score.IsPerfectGame)
            stats.PerfectGames++;

        if (stats.BestScore is null || score.CardsRemaining < stats.BestScore.Value)
            stats.BestScore = score.CardsRemaining;

        stats.AverageRemainingCards = previousTotal == 0
            ? score.CardsRemaining
            : Math.Round(((previousAvg * previousTotal) + score.CardsRemaining) / (previousTotal + 1), 2);

        if (durationMinutes is int minutes)
            stats.TotalPlayTimeMinutes += Math.Max(minutes, 0);

        stats.LastUpdated = DateTime.UtcNow;
    }

    private static GameStateView BuildView(GameSession session, GameState state, IList<int> hand, IList<int> drawPile, GameScore? finalScore)
    {
        var minCards = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);
        var piles = new PileTops(state.AscendingPile1, state.AscendingPile2, state.DescendingPile1, state.DescendingPile2);

        return new GameStateView(
            SessionId: session.Id,
            GamePhase: session.GamePhase,
            IsExpertMode: session.IsExpertMode,
            Piles: piles,
            DrawPileCount: drawPile.Count,
            PlayedCardsCount: state.PlayedCardsCount,
            Hand: hand,
            MinCardsThisTurn: minCards,
            FinalScore: finalScore);
    }

    private record GameContext(
        GameSession? Session,
        GameState? State,
        GamePlayer? Player,
        List<int>? Hand,
        List<int>? DrawPile,
        string? Error)
    {
        public static GameContext Failed(string error) => new(null, null, null, null, null, error);
    }
}
