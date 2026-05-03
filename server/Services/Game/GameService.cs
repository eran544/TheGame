using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;

namespace TheGameServer.Services.Game;

public interface IGameService
{
    // Single-player
    Task<GameResultEnvelope<GameStateView>> StartSinglePlayerGameAsync(Guid userId, bool isExpertMode = false);
    Task<GameResultEnvelope<TurnOutcome>> PlayTurnAsync(Guid sessionId, Guid userId, IList<CardPlay> plays);
    Task<GameResultEnvelope<GameStateView>> GetGameStateAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<bool>> AbandonGameAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<GameStateView>> UndoLastMoveAsync(Guid sessionId, Guid userId);

    // Multiplayer lobby
    Task<GameResultEnvelope<LobbyView>> CreateMultiplayerGameAsync(Guid userId, int maxPlayers, bool isExpertMode = false);
    Task<GameResultEnvelope<LobbyView>> JoinGameAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<GameStateView>> StartMultiplayerGameAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<LeaveResult>> LeaveGameAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<LobbyView>> GetLobbyStateAsync(Guid sessionId, Guid userId);
}

public record LeaveResult(bool GameEnded);

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
    GameScore? FinalScore,
    bool CanUndo,
    Guid? CurrentPlayerId = null,
    IList<PlayerInGame>? Players = null,
    LastMove? LastMove = null);

public record TurnOutcome(GameStateView State, bool GameEnded, string? EndReason);

public record PlayerInGame(Guid UserId, string Username, int HandCount, bool IsAI, bool IsCurrentTurn, bool IsDisconnected);
public record LastMovePlay(int Card, int PileSlot);
public record LastMove(string PlayerUsername, IList<LastMovePlay> Plays);
public record LobbyPlayer(Guid UserId, string Username, int PlayerIndex, bool IsAI);
public record LobbyView(Guid SessionId, string GamePhase, IList<LobbyPlayer> Players, int MaxPlayers, bool IsExpertMode, bool CanStart, Guid CreatedBy);

// Stored as JSON in GameState.UndoSnapshotJson — captures everything needed to reverse one card play.
file record UndoSnapshot(
    int Asc1, int Asc2, int Desc1, int Desc2,
    int PlayedCard,
    int? DrawnCard,
    int PlayedCardsCountBefore);

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

    // ── Single-player ───────────────────────────────────────────────────────

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
            CurrentPlayerId = userId,
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

        var isSinglePlayer = session.MaxPlayers == 1;

        if (!isSinglePlayer && state.CurrentPlayerId != userId)
            return GameResultEnvelope<TurnOutcome>.Fail("It is not your turn");

        var minCards = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);

        var piles = new PileTops(state.AscendingPile1, state.AscendingPile2, state.DescendingPile1, state.DescendingPile2);
        var validation = _engine.ValidateTurn(plays, hand, piles, minCards);
        if (!validation.IsValid)
            return GameResultEnvelope<TurnOutcome>.Fail(validation.Error!);

        var newHand = validation.ResultingHand.ToList();
        var newPiles = validation.ResultingPiles;

        int? drawnCard = null;
        var refillCount = drawPile.Count == 0 ? 0 : Math.Min(plays.Count, drawPile.Count);
        if (refillCount > 0)
        {
            drawnCard = drawPile[0];
            for (var i = 0; i < refillCount; i++)
            {
                newHand.Add(drawPile[0]);
                drawPile.RemoveAt(0);
            }
        }

        if (isSinglePlayer && plays.Count == 1)
        {
            var snapshot = new UndoSnapshot(
                state.AscendingPile1, state.AscendingPile2,
                state.DescendingPile1, state.DescendingPile2,
                plays[0].Card,
                drawnCard,
                state.PlayedCardsCount);
            state.UndoSnapshotJson = JsonSerializer.Serialize(snapshot);
        }
        else
        {
            state.UndoSnapshotJson = null;
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

        var perfectGame = newHand.Count == 0 && drawPile.Count == 0
            && session.Players.Where(p => !p.IsSpectator && p.UserId != userId)
                .All(p => (JsonSerializer.Deserialize<List<int>>(p.Hand?.Cards ?? "[]") ?? new()).Count == 0);

        GameScore? finalScore = null;
        var gameEnded = false;
        string? endReason = null;

        if (isSinglePlayer)
        {
            var nextMin = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);
            var canContinue = !perfectGame && _engine.CanPlayMinimumCards(newHand, newPiles, nextMin);
            gameEnded = perfectGame || !canContinue;
        }
        else
        {
            if (perfectGame)
            {
                gameEnded = true;
            }
            else
            {
                // Advance turn to next active player and check if they can play
                var activePlayers = session.Players
                    .Where(p => !p.IsSpectator && p.DisconnectedAt == null)
                    .OrderBy(p => p.PlayerIndex)
                    .ToList();
                var currentIdx = activePlayers.FindIndex(p => p.UserId == userId);
                var nextPlayer = activePlayers[(currentIdx + 1) % activePlayers.Count];
                state.CurrentPlayerId = nextPlayer.UserId;

                var nextHand = nextPlayer.Hand is not null
                    ? JsonSerializer.Deserialize<List<int>>(nextPlayer.Hand.Cards) ?? new()
                    : new List<int>();
                var nextMin = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);
                if (!_engine.CanPlayMinimumCards(nextHand, newPiles, nextMin))
                    gameEnded = true;
            }
        }

        if (gameEnded)
        {
            endReason = perfectGame ? "completed" : "completed";
            state.UndoSnapshotJson = null;
            session.GamePhase = "ended";
            session.EndedAt = DateTime.UtcNow;

            var durationMinutes = session.StartedAt is null ? (int?)null
                : (int)Math.Round((session.EndedAt!.Value - session.StartedAt.Value).TotalMinutes);

            var totalCardsRemaining = isSinglePlayer
                ? newHand.Count + drawPile.Count
                : session.Players.Where(p => !p.IsSpectator)
                    .Sum(p =>
                    {
                        var cards = p.UserId == userId ? newHand
                            : JsonSerializer.Deserialize<List<int>>(p.Hand?.Cards ?? "[]") ?? new();
                        return cards.Count;
                    }) + drawPile.Count;

            finalScore = _engine.CalculateScore(totalCardsRemaining);

            var result = new GameResult
            {
                GameSessionId = session.Id,
                TotalCardsRemaining = totalCardsRemaining,
                IsPerfectGame = finalScore.IsPerfectGame,
                GameDurationMinutes = durationMinutes,
                EndReason = endReason
            };
            _db.GameResults.Add(result);

            if (isSinglePlayer)
            {
                _db.PlayerGameStats.Add(new PlayerGameStat
                {
                    GameResultId = result.Id,
                    UserId = userId,
                    CardsInHand = newHand.Count,
                    PlayTimeMinutes = durationMinutes
                });
                await UpdatePlayerStatisticsAsync(userId, finalScore, durationMinutes);
            }
            else
            {
                foreach (var mp in session.Players.Where(p => !p.IsAI && !p.IsSpectator))
                {
                    var mpHandCount = mp.UserId == userId ? newHand.Count
                        : (JsonSerializer.Deserialize<List<int>>(mp.Hand?.Cards ?? "[]") ?? new()).Count;
                    _db.PlayerGameStats.Add(new PlayerGameStat
                    {
                        GameResultId = result.Id,
                        UserId = mp.UserId,
                        CardsInHand = mpHandCount,
                        PlayTimeMinutes = durationMinutes
                    });
                    await UpdatePlayerStatisticsAsync(mp.UserId, finalScore, durationMinutes);
                }
            }
        }

        var actingUsername = player.User?.Username ?? "Unknown";
        state.MoveHistory.Add(new MoveHistoryEntry
        {
            PlayerUsername = actingUsername,
            Plays = plays.Select(p => new MoveHistoryPlay { Card = p.Card, PileSlot = (int)p.Slot }).ToList()
        });

        await _db.SaveChangesAsync();

        var lastMove = new LastMove(actingUsername,
            plays.Select(p => new LastMovePlay(p.Card, (int)p.Slot)).ToList());

        var allPlayers = BuildPlayersInGame(session, state, userId, newHand);
        var view = BuildView(session, state, newHand, drawPile, finalScore, allPlayers, lastMove);
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

        var allPlayers = BuildPlayersInGame(context.Session, context.State!, userId, context.Hand!);
        var view = BuildView(context.Session, context.State!, context.Hand!, context.DrawPile!, finalScore, allPlayers);
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

    public async Task<GameResultEnvelope<GameStateView>> UndoLastMoveAsync(Guid sessionId, Guid userId)
    {
        var context = await LoadGameContextAsync(sessionId, userId);
        if (context.Error is not null)
            return GameResultEnvelope<GameStateView>.Fail(context.Error);

        var session = context.Session!;
        var state = context.State!;
        var player = context.Player!;
        var hand = context.Hand!;
        var drawPile = context.DrawPile!;

        if (session.GamePhase != "playing")
            return GameResultEnvelope<GameStateView>.Fail("Game is not in progress");

        if (session.MaxPlayers != 1)
            return GameResultEnvelope<GameStateView>.Fail("Undo is only available in single-player mode");

        if (state.UndoSnapshotJson is null)
            return GameResultEnvelope<GameStateView>.Fail("No move to undo");

        var snapshot = JsonSerializer.Deserialize<UndoSnapshot>(state.UndoSnapshotJson)
            ?? throw new InvalidOperationException("Failed to deserialize undo snapshot");

        state.AscendingPile1 = snapshot.Asc1;
        state.AscendingPile2 = snapshot.Asc2;
        state.DescendingPile1 = snapshot.Desc1;
        state.DescendingPile2 = snapshot.Desc2;
        state.PlayedCardsCount = snapshot.PlayedCardsCountBefore;

        hand.Remove(snapshot.PlayedCard);
        hand.Add(snapshot.PlayedCard);
        if (snapshot.DrawnCard is int replaced)
        {
            hand.Remove(replaced);
            drawPile.Insert(0, replaced);
        }

        state.DrawPileCards = JsonSerializer.Serialize(drawPile);
        state.UndoSnapshotJson = null;
        state.UpdatedAt = DateTime.UtcNow;

        player.Hand!.Cards = JsonSerializer.Serialize(hand);
        player.Hand.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return GameResultEnvelope<GameStateView>.Ok(BuildView(session, state, hand, drawPile, finalScore: null));
    }

    // ── Multiplayer lobby ───────────────────────────────────────────────────

    public async Task<GameResultEnvelope<LobbyView>> CreateMultiplayerGameAsync(Guid userId, int maxPlayers, bool isExpertMode = false)
    {
        if (maxPlayers < 2 || maxPlayers > GameRules.MaxPlayers)
            return GameResultEnvelope<LobbyView>.Fail($"Player count must be between 2 and {GameRules.MaxPlayers}");

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null) return GameResultEnvelope<LobbyView>.Fail("User not found");

        var session = new GameSession
        {
            CreatedBy = userId,
            GamePhase = "lobby",
            MaxPlayers = maxPlayers,
            IsExpertMode = isExpertMode
        };

        var player = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = userId,
            PlayerIndex = 0
        };

        _db.GameSessions.Add(session);
        _db.GamePlayers.Add(player);
        await _db.SaveChangesAsync();

        return GameResultEnvelope<LobbyView>.Ok(BuildLobbyView(session, new[] { (player, user) }));
    }

    public async Task<GameResultEnvelope<LobbyView>> JoinGameAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<LobbyView>.Fail("Game session not found");
        if (session.GamePhase != "lobby") return GameResultEnvelope<LobbyView>.Fail("Game has already started");
        if (session.Players.Any(p => p.UserId == userId)) return GameResultEnvelope<LobbyView>.Fail("Already in this game");
        if (session.Players.Count(p => !p.IsSpectator) >= session.MaxPlayers) return GameResultEnvelope<LobbyView>.Fail("Game is full");

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null) return GameResultEnvelope<LobbyView>.Fail("User not found");

        var player = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = userId,
            PlayerIndex = session.Players.Count
        };

        _db.GamePlayers.Add(player);
        await _db.SaveChangesAsync();

        // EF Core already added player to session.Players via navigation fixup;
        // set User manually since it wasn't included in the ThenInclude.
        player.User = user;
        return GameResultEnvelope<LobbyView>.Ok(BuildLobbyView(session, session.Players.Select(p => (p, p.User!))));
    }

    public async Task<GameResultEnvelope<GameStateView>> StartMultiplayerGameAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<GameStateView>.Fail("Game session not found");
        if (session.CreatedBy != userId) return GameResultEnvelope<GameStateView>.Fail("Only the creator can start the game");
        if (session.GamePhase != "lobby") return GameResultEnvelope<GameStateView>.Fail("Game is not in lobby phase");

        var activePlayers = session.Players.Where(p => !p.IsSpectator).OrderBy(p => p.PlayerIndex).ToList();
        if (activePlayers.Count < 2) return GameResultEnvelope<GameStateView>.Fail("Need at least 2 players to start");

        var deck = _shuffler.Shuffle();
        var handSize = GameRules.GetInitialHandSize(activePlayers.Count, session.IsExpertMode);
        var deckOffset = 0;

        var handsByPlayer = new Dictionary<Guid, List<int>>();
        foreach (var p in activePlayers)
        {
            var hand = deck.Skip(deckOffset).Take(handSize).ToList();
            deckOffset += handSize;
            handsByPlayer[p.Id] = hand;

            _db.PlayerHands.Add(new PlayerHand
            {
                GameSessionId = session.Id,
                PlayerId = p.Id,
                Cards = JsonSerializer.Serialize(hand)
            });
        }

        var drawPile = deck.Skip(deckOffset).ToList();
        var firstPlayer = activePlayers.First();

        var state = new GameState
        {
            GameSessionId = session.Id,
            CurrentPlayerId = firstPlayer.UserId,
            AscendingPile1 = GameRules.AscendingStartValue,
            AscendingPile2 = GameRules.AscendingStartValue,
            DescendingPile1 = GameRules.DescendingStartValue,
            DescendingPile2 = GameRules.DescendingStartValue,
            DrawPileCards = JsonSerializer.Serialize(drawPile),
            PlayedCardsCount = 0
        };

        session.GamePhase = "playing";
        session.StartedAt = DateTime.UtcNow;

        _db.GameStates.Add(state);
        await _db.SaveChangesAsync();

        // Reload hands for the view
        await _db.Entry(session).Collection(s => s.Players)
            .Query().Include(p => p.Hand).Include(p => p.User).LoadAsync();

        var requestingPlayer = session.Players.First(p => p.UserId == userId);
        var requestingHand = handsByPlayer[requestingPlayer.Id];
        var allPlayersView = BuildPlayersInGame(session, state, userId, requestingHand);

        return GameResultEnvelope<GameStateView>.Ok(BuildView(session, state, requestingHand, drawPile, null, allPlayersView));
    }

    public async Task<GameResultEnvelope<LeaveResult>> LeaveGameAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.State)
            .Include(s => s.Players).ThenInclude(p => p.Hand)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<LeaveResult>.Fail("Game session not found");

        var player = session.Players.SingleOrDefault(p => p.UserId == userId);
        if (player is null) return GameResultEnvelope<LeaveResult>.Fail("Not in this game");

        if (session.GamePhase == "lobby")
        {
            _db.GamePlayers.Remove(player);
            var remaining = session.Players.Where(p => p.UserId != userId).ToList();
            if (!remaining.Any())
            {
                session.GamePhase = "ended";
                session.EndedAt = DateTime.UtcNow;
            }
            else if (session.CreatedBy == userId)
            {
                session.CreatedBy = remaining.OrderBy(p => p.PlayerIndex).First().UserId;
            }
            await _db.SaveChangesAsync();
            return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(false));
        }

        if (session.GamePhase == "playing")
        {
            // Phase 2: any leave during game ends it without saving stats
            session.GamePhase = "ended";
            session.EndedAt = DateTime.UtcNow;
            var alreadyEnded = await _db.GameResults.AnyAsync(r => r.GameSessionId == sessionId);
            if (!alreadyEnded)
            {
                _db.GameResults.Add(new GameResult
                {
                    GameSessionId = sessionId,
                    TotalCardsRemaining = 0,
                    IsPerfectGame = false,
                    EndReason = "disconnection"
                });
            }
            await _db.SaveChangesAsync();
            return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(true));
        }

        // Game already ended — no-op, don't broadcast again
        return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(false));
    }

    public async Task<GameResultEnvelope<LobbyView>> GetLobbyStateAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<LobbyView>.Fail("Game session not found");
        if (session.Players.All(p => p.UserId != userId)) return GameResultEnvelope<LobbyView>.Fail("Not in this game");
        if (session.GamePhase != "lobby") return GameResultEnvelope<LobbyView>.Fail("Game is not in lobby phase");

        return GameResultEnvelope<LobbyView>.Ok(BuildLobbyView(session, session.Players.Select(p => (p, p.User!))));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<GameContext> LoadGameContextAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.State)
            .Include(s => s.Players).ThenInclude(p => p.Hand)
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameContext.Failed("Game session not found");
        if (session.State is null) return GameContext.Failed("Game state not initialized");

        var player = session.Players.SingleOrDefault(p => p.UserId == userId);
        if (player is null) return GameContext.Failed("Player is not part of this game");
        if (player.Hand is null) return GameContext.Failed("Player hand not initialized");

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
        if (score.IsPerfectGame) stats.PerfectGames++;

        if (stats.BestScore is null || score.CardsRemaining < stats.BestScore.Value)
            stats.BestScore = score.CardsRemaining;

        stats.AverageRemainingCards = previousTotal == 0
            ? score.CardsRemaining
            : Math.Round(((previousAvg * previousTotal) + score.CardsRemaining) / (previousTotal + 1), 2);

        if (durationMinutes is int minutes)
            stats.TotalPlayTimeMinutes += Math.Max(minutes, 0);

        stats.LastUpdated = DateTime.UtcNow;
    }

    private static IList<PlayerInGame>? BuildPlayersInGame(GameSession session, GameState state, Guid requestingUserId, IList<int> requestingHand)
    {
        if (session.MaxPlayers == 1) return null;

        return session.Players
            .Where(p => !p.IsSpectator)
            .OrderBy(p => p.PlayerIndex)
            .Select(p =>
            {
                var handCount = p.UserId == requestingUserId
                    ? requestingHand.Count
                    : (p.Hand is not null ? (JsonSerializer.Deserialize<List<int>>(p.Hand.Cards)?.Count ?? 0) : 0);
                return new PlayerInGame(
                    UserId: p.UserId,
                    Username: p.User?.Username ?? "Unknown",
                    HandCount: handCount,
                    IsAI: p.IsAI,
                    IsCurrentTurn: state.CurrentPlayerId == p.UserId,
                    IsDisconnected: p.DisconnectedAt is not null);
            })
            .ToList();
    }

    private static LobbyView BuildLobbyView(GameSession session, IEnumerable<(GamePlayer player, User user)> players)
    {
        var list = players
            .OrderBy(t => t.player.PlayerIndex)
            .Select(t => new LobbyPlayer(t.user.Id, t.user.Username, t.player.PlayerIndex, t.player.IsAI))
            .ToList();

        return new LobbyView(
            SessionId: session.Id,
            GamePhase: session.GamePhase,
            Players: list,
            MaxPlayers: session.MaxPlayers,
            IsExpertMode: session.IsExpertMode,
            CanStart: list.Count >= 2,
            CreatedBy: session.CreatedBy);
    }

    private static GameStateView BuildView(
        GameSession session,
        GameState state,
        IList<int> hand,
        IList<int> drawPile,
        GameScore? finalScore,
        IList<PlayerInGame>? players = null,
        LastMove? lastMove = null)
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
            FinalScore: finalScore,
            CanUndo: state.UndoSnapshotJson is not null,
            CurrentPlayerId: state.CurrentPlayerId,
            Players: players,
            LastMove: lastMove);
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
