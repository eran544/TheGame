using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    Task<GameResultEnvelope<LobbyView>> AddAIPlayerAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<LobbyView>> RemoveAIPlayerAsync(Guid sessionId, Guid userId, Guid aiUserId);

    // Disconnection & reconnection
    Task<GameResultEnvelope<ReconnectResult>> ReconnectPlayerAsync(Guid sessionId, Guid userId);
    Task<GameResultEnvelope<TimeoutResult>> TimeoutCurrentPlayerAsync(Guid sessionId);
}

public record LeaveResult(
    bool GameEnded,
    string? DisconnectedUsername = null,
    string? ReplacedByAIUsername = null,
    GameStateView? StateAfterReplacement = null);

public record ReconnectResult(string ReconnectedUsername, GameStateView State);
public record TimeoutResult(string DisconnectedUsername, string ReplacedByAIUsername, GameStateView? State, bool GameEnded, string? EndReason);

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
    IList<LastMove>? RecentMoves = null);

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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GameService> _logger;

    private static readonly JsonSerializerOptions _camelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private record AiServiceResponse(
        [property: JsonPropertyName("plays")] List<AiServiceCardPlay> Plays,
        [property: JsonPropertyName("source")] string Source);

    private record AiServiceCardPlay(
        [property: JsonPropertyName("card")] int Card,
        [property: JsonPropertyName("pileSlot")] int PileSlot);

    private record AiTurnResult(bool GameEnded, string? EndReason, GameScore? FinalScore, IList<LastMove> AIMoves);

    public GameService(AppDbContext db, IGameEngine engine, IDeckShuffler shuffler,
        IHttpClientFactory httpClientFactory, ILogger<GameService> logger)
    {
        _db = db;
        _engine = engine;
        _shuffler = shuffler;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
        state.CurrentTurnStartedAt = null; // will be set when next player is determined

        player.Hand!.Cards = JsonSerializer.Serialize(newHand);
        player.Hand.UpdatedAt = DateTime.UtcNow;

        var perfectGame = newHand.Count == 0 && drawPile.Count == 0
            && session.Players.Where(p => !p.IsSpectator && p.UserId != userId)
                .All(p => (JsonSerializer.Deserialize<List<int>>(p.Hand?.Cards ?? "[]") ?? new()).Count == 0);

        GameScore? finalScore = null;
        var gameEnded = false;
        string? endReason = null;
        GamePlayer? nextPlayerForAI = null;

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
                var activePlayers = session.Players
                    .Where(p => !p.IsSpectator && p.DisconnectedAt == null)
                    .OrderBy(p => p.PlayerIndex)
                    .ToList();
                var currentIdx = activePlayers.FindIndex(p => p.UserId == userId);
                nextPlayerForAI = activePlayers[(currentIdx + 1) % activePlayers.Count];
                state.CurrentPlayerId = nextPlayerForAI.UserId;
                state.CurrentTurnStartedAt = DateTime.UtcNow;

                var nextHand = nextPlayerForAI.Hand is not null
                    ? JsonSerializer.Deserialize<List<int>>(nextPlayerForAI.Hand.Cards) ?? new()
                    : new List<int>();
                var nextMin = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);
                if (!_engine.CanPlayMinimumCards(nextHand, newPiles, nextMin))
                    gameEnded = true;
            }
        }

        if (gameEnded)
        {
            endReason = "completed";
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

        var humanMove = new LastMove(actingUsername,
            plays.Select(p => new LastMovePlay(p.Card, (int)p.Slot)).ToList());
        var recentMoves = isSinglePlayer ? null : new List<LastMove> { humanMove };

        // After human's turn, execute any consecutive AI turns
        if (!gameEnded && !isSinglePlayer && nextPlayerForAI?.IsAI == true)
        {
            var aiResult = await HandleAITurnsAsync(session, state, drawPile);
            if (aiResult.GameEnded)
            {
                gameEnded = true;
                endReason = aiResult.EndReason;
                finalScore = aiResult.FinalScore;
            }
            recentMoves!.AddRange(aiResult.AIMoves);
        }

        var allPlayers = BuildPlayersInGame(session, state, userId, newHand);
        var view = BuildView(session, state, newHand, drawPile, finalScore, allPlayers, recentMoves);
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
        var requestingUsername = context.Player!.User?.Username;
        var recentMoves = context.Session.MaxPlayers > 1
            ? ComputeRecentMoves(context.State!.MoveHistory, requestingUsername)
            : null;
        var view = BuildView(context.Session, context.State!, context.Hand!, context.DrawPile!, finalScore, allPlayers, recentMoves);
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
            PlayedCardsCount = 0,
            CurrentTurnStartedAt = firstPlayer.IsAI ? null : DateTime.UtcNow
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

        // If the first player is AI, trigger AI turns immediately
        if (firstPlayer.IsAI)
        {
            var aiResult = await HandleAITurnsAsync(session, state, drawPile);
            allPlayersView = BuildPlayersInGame(session, state, userId, requestingHand);
            if (aiResult.GameEnded)
            {
                var view = BuildView(session, state, requestingHand, drawPile,
                    aiResult.FinalScore, allPlayersView, aiResult.AIMoves);
                return GameResultEnvelope<GameStateView>.Ok(view);
            }
        }

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
                session.CreatedBy = remaining.Where(p => !p.IsAI).OrderBy(p => p.PlayerIndex).FirstOrDefault()?.UserId
                    ?? remaining.OrderBy(p => p.PlayerIndex).First().UserId;
            }
            await _db.SaveChangesAsync();
            return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(false));
        }

        if (session.GamePhase == "playing")
        {
            var disconnectedUsername = player.User?.Username ?? "Player";

            // Count remaining humans after this player disconnects
            var remainingHumans = session.Players
                .Where(p => p.UserId != userId && !p.IsAI && !p.IsSpectator && p.DisconnectedAt == null)
                .ToList();

            if (remainingHumans.Count == 0)
            {
                // No humans left — end the game
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
                return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(true, disconnectedUsername));
            }

            // Replace disconnected player with AI so the game continues
            var (aiUsername, replacementOk) = await ReplaceWithAIAsync(session, session.State!, userId);
            if (!replacementOk)
            {
                // No AI slot available — fall back to ending the game
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
                return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(true, disconnectedUsername));
            }

            // If it was the disconnected player's turn, the AI now holds the turn.
            // Run AI turns and build a broadcast state from the first remaining human's perspective.
            GameStateView? stateView = null;
            var state = session.State!;
            var drawPile = JsonSerializer.Deserialize<List<int>>(state.DrawPileCards) ?? [];

            if (state.CurrentPlayerId != null && session.Players.Any(p => p.UserId == state.CurrentPlayerId && p.IsAI))
            {
                var aiResult = await HandleAITurnsAsync(session, state, drawPile);
                if (aiResult.GameEnded)
                {
                    return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(
                        true, disconnectedUsername, aiUsername,
                        BuildViewForBroadcast(session, state, drawPile, aiResult.FinalScore)));
                }
            }

            stateView = BuildViewForBroadcast(session, state, drawPile, null);
            return GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(false, disconnectedUsername, aiUsername, stateView));
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

    public async Task<GameResultEnvelope<LobbyView>> AddAIPlayerAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<LobbyView>.Fail("Game session not found");
        if (session.GamePhase != "lobby") return GameResultEnvelope<LobbyView>.Fail("Game has already started");
        if (session.CreatedBy != userId) return GameResultEnvelope<LobbyView>.Fail("Only the host can add AI players");
        if (session.Players.Count(p => !p.IsSpectator) >= session.MaxPlayers)
            return GameResultEnvelope<LobbyView>.Fail("Game is full");

        // Find an AI user not already in this session
        var usedAiIds = session.Players.Where(p => p.IsAI).Select(p => p.UserId).ToHashSet();
        var availableAiId = AiPlayerConstants.Ids.FirstOrDefault(id => !usedAiIds.Contains(id));
        if (availableAiId == Guid.Empty)
            return GameResultEnvelope<LobbyView>.Fail("No AI players available");

        var aiUser = await _db.Users.SingleOrDefaultAsync(u => u.Id == availableAiId);
        if (aiUser is null)
            return GameResultEnvelope<LobbyView>.Fail("AI player account not initialized. Restart the server.");

        var aiPlayer = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = availableAiId,
            PlayerIndex = session.Players.Count,
            IsAI = true
        };

        _db.GamePlayers.Add(aiPlayer);
        await _db.SaveChangesAsync();

        aiPlayer.User = aiUser;
        return GameResultEnvelope<LobbyView>.Ok(BuildLobbyView(session, session.Players.Select(p => (p, p.User!))));
    }

    public async Task<GameResultEnvelope<LobbyView>> RemoveAIPlayerAsync(Guid sessionId, Guid userId, Guid aiUserId)
    {
        var session = await _db.GameSessions
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<LobbyView>.Fail("Game session not found");
        if (session.GamePhase != "lobby") return GameResultEnvelope<LobbyView>.Fail("Game has already started");
        if (session.CreatedBy != userId) return GameResultEnvelope<LobbyView>.Fail("Only the host can remove AI players");

        var aiPlayer = session.Players.SingleOrDefault(p => p.UserId == aiUserId && p.IsAI);
        if (aiPlayer is null) return GameResultEnvelope<LobbyView>.Fail("AI player not found in lobby");

        _db.GamePlayers.Remove(aiPlayer);
        await _db.SaveChangesAsync();

        var remaining = session.Players.Where(p => p.Id != aiPlayer.Id);
        return GameResultEnvelope<LobbyView>.Ok(BuildLobbyView(session, remaining.Select(p => (p, p.User!))));
    }

    // ── Disconnection & reconnection ────────────────────────────────────────

    public async Task<GameResultEnvelope<ReconnectResult>> ReconnectPlayerAsync(Guid sessionId, Guid userId)
    {
        var session = await _db.GameSessions
            .Include(s => s.State)
            .Include(s => s.Players).ThenInclude(p => p.Hand)
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<ReconnectResult>.Fail("Game session not found");
        if (session.GamePhase != "playing") return GameResultEnvelope<ReconnectResult>.Fail("Game is not in progress");

        var humanPlayer = session.Players.SingleOrDefault(p => p.UserId == userId && p.DisconnectedAt != null && p.ReplacedByAI);
        if (humanPlayer is null) return GameResultEnvelope<ReconnectResult>.Fail("No active disconnection found for this player");

        // Find the AI that replaced them (same PlayerIndex, IsAI, not disconnected)
        var aiPlayer = session.Players.SingleOrDefault(p =>
            p.IsAI && p.PlayerIndex == humanPlayer.PlayerIndex && p.DisconnectedAt == null);
        if (aiPlayer is null) return GameResultEnvelope<ReconnectResult>.Fail("Replacement AI player not found");

        // Transfer current hand from AI back to the human
        var aiHandJson = aiPlayer.Hand?.Cards ?? "[]";
        if (humanPlayer.Hand is not null)
            humanPlayer.Hand.Cards = aiHandJson;
        else
            _db.PlayerHands.Add(new PlayerHand { GameSessionId = session.Id, PlayerId = humanPlayer.Id, Cards = aiHandJson });

        humanPlayer.DisconnectedAt = null;
        humanPlayer.ReplacedByAI = false;

        var state = session.State!;
        // If it's the AI's turn, hand it back to the human
        if (state.CurrentPlayerId == aiPlayer.UserId)
        {
            state.CurrentPlayerId = userId;
            state.CurrentTurnStartedAt = DateTime.UtcNow;
        }

        _db.GamePlayers.Remove(aiPlayer);
        await _db.SaveChangesAsync();

        var hand = JsonSerializer.Deserialize<List<int>>(aiHandJson) ?? [];
        var drawPile = JsonSerializer.Deserialize<List<int>>(state.DrawPileCards) ?? [];
        var allPlayers = BuildPlayersInGame(session, state, userId, hand);
        var stateView = BuildView(session, state, hand, drawPile, null, allPlayers);
        var username = humanPlayer.User?.Username ?? "Player";

        return GameResultEnvelope<ReconnectResult>.Ok(new ReconnectResult(username, stateView));
    }

    public async Task<GameResultEnvelope<TimeoutResult>> TimeoutCurrentPlayerAsync(Guid sessionId)
    {
        var session = await _db.GameSessions
            .Include(s => s.State)
            .Include(s => s.Players).ThenInclude(p => p.Hand)
            .Include(s => s.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return GameResultEnvelope<TimeoutResult>.Fail("Game session not found");
        if (session.GamePhase != "playing") return GameResultEnvelope<TimeoutResult>.Fail("Game not in progress");

        var state = session.State!;
        var currentPlayer = session.Players.SingleOrDefault(p =>
            p.UserId == state.CurrentPlayerId && !p.IsAI && p.DisconnectedAt == null);
        if (currentPlayer is null) return GameResultEnvelope<TimeoutResult>.Fail("Current player is not a timed-out human");

        var disconnectedUsername = currentPlayer.User?.Username ?? "Player";

        var remainingHumans = session.Players
            .Where(p => p.UserId != currentPlayer.UserId && !p.IsAI && !p.IsSpectator && p.DisconnectedAt == null)
            .ToList();

        if (remainingHumans.Count == 0)
        {
            session.GamePhase = "ended";
            session.EndedAt = DateTime.UtcNow;
            var alreadyEnded = await _db.GameResults.AnyAsync(r => r.GameSessionId == sessionId);
            if (!alreadyEnded)
                _db.GameResults.Add(new GameResult { GameSessionId = sessionId, TotalCardsRemaining = 0, IsPerfectGame = false, EndReason = "disconnection" });
            await _db.SaveChangesAsync();
            return GameResultEnvelope<TimeoutResult>.Ok(new TimeoutResult(disconnectedUsername, "", null, true, "disconnection"));
        }

        var (aiUsername, replacementOk) = await ReplaceWithAIAsync(session, state, currentPlayer.UserId);
        if (!replacementOk)
        {
            session.GamePhase = "ended";
            session.EndedAt = DateTime.UtcNow;
            var alreadyEnded = await _db.GameResults.AnyAsync(r => r.GameSessionId == sessionId);
            if (!alreadyEnded)
                _db.GameResults.Add(new GameResult { GameSessionId = sessionId, TotalCardsRemaining = 0, IsPerfectGame = false, EndReason = "disconnection" });
            await _db.SaveChangesAsync();
            return GameResultEnvelope<TimeoutResult>.Ok(new TimeoutResult(disconnectedUsername, "", null, true, "disconnection"));
        }

        var drawPile = JsonSerializer.Deserialize<List<int>>(state.DrawPileCards) ?? [];
        var aiResult = await HandleAITurnsAsync(session, state, drawPile);
        var broadcastState = BuildViewForBroadcast(session, state, drawPile, aiResult.FinalScore);

        return GameResultEnvelope<TimeoutResult>.Ok(new TimeoutResult(
            disconnectedUsername, aiUsername, broadcastState, aiResult.GameEnded, aiResult.EndReason));
    }

    // Replaces a disconnected human player with an AI. Returns (aiUsername, success).
    private async Task<(string AiUsername, bool Success)> ReplaceWithAIAsync(
        GameSession session, GameState state, Guid disconnectedUserId)
    {
        var player = session.Players.Single(p => p.UserId == disconnectedUserId);
        player.DisconnectedAt = DateTime.UtcNow;
        player.ReplacedByAI = true;

        var usedAiIds = session.Players.Where(p => p.IsAI).Select(p => p.UserId).ToHashSet();
        var availableAiId = AiPlayerConstants.Ids.FirstOrDefault(id => !usedAiIds.Contains(id));
        if (availableAiId == Guid.Empty)
            return (string.Empty, false);

        var aiUser = await _db.Users.SingleOrDefaultAsync(u => u.Id == availableAiId);
        if (aiUser is null) return (string.Empty, false);

        var aiPlayer = new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = availableAiId,
            PlayerIndex = player.PlayerIndex,
            IsAI = true
        };
        _db.GamePlayers.Add(aiPlayer);

        var handJson = player.Hand?.Cards ?? "[]";
        var aiHand = new PlayerHand { GameSessionId = session.Id, PlayerId = aiPlayer.Id, Cards = handJson };
        _db.PlayerHands.Add(aiHand);

        if (state.CurrentPlayerId == disconnectedUserId)
        {
            state.CurrentPlayerId = availableAiId;
            state.CurrentTurnStartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // EF relationship fixup already added aiPlayer to session.Players.
        aiPlayer.User = aiUser;
        aiPlayer.Hand = aiHand;

        _logger.LogInformation("Player {DisconnectedUser} replaced by AI {AIUser} in session {SessionId}",
            player.User?.Username ?? disconnectedUserId.ToString(), aiUser.Username, session.Id);

        return (aiUser.Username, true);
    }

    // Builds a game state view without a specific player's hand (safe to broadcast to the whole group).
    private static GameStateView BuildViewForBroadcast(
        GameSession session, GameState state, List<int> drawPile, GameScore? finalScore)
    {
        var activePlayers = session.Players
            .Where(p => !p.IsSpectator)
            .OrderBy(p => p.PlayerIndex)
            .Select(p =>
            {
                var count = p.Hand is not null
                    ? (JsonSerializer.Deserialize<List<int>>(p.Hand.Cards)?.Count ?? 0)
                    : 0;
                return new PlayerInGame(p.UserId, p.User?.Username ?? "AI", count, p.IsAI,
                    state.CurrentPlayerId == p.UserId, p.DisconnectedAt is not null);
            })
            .ToList<PlayerInGame>();

        return BuildView(session, state, [], drawPile, finalScore, activePlayers);
    }

    // ── AI turn handling ────────────────────────────────────────────────────

    private async Task<AiTurnResult> HandleAITurnsAsync(GameSession session, GameState state, List<int> drawPile)
    {
        var aiMoves = new List<LastMove>();

        while (true)
        {
            var activePlayers = session.Players
                .Where(p => !p.IsSpectator && p.DisconnectedAt == null)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            var currentPlayer = activePlayers.SingleOrDefault(p => p.UserId == state.CurrentPlayerId);
            if (currentPlayer is null || !currentPlayer.IsAI)
                return new AiTurnResult(false, null, null, aiMoves);

            var aiHand = currentPlayer.Hand is not null
                ? JsonSerializer.Deserialize<List<int>>(currentPlayer.Hand.Cards) ?? []
                : [];

            var piles = new PileTops(state.AscendingPile1, state.AscendingPile2, state.DescendingPile1, state.DescendingPile2);
            var minCards = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);

            if (!_engine.CanPlayMinimumCards(aiHand, piles, minCards))
            {
                var score = await FinalizeMultiplayerGameAsync(session, state, drawPile, "completed");
                await _db.SaveChangesAsync();
                return new AiTurnResult(true, "completed", score, aiMoves);
            }

            // Get AI plays from service (or local greedy fallback)
            var aiPlays = await CallAiServiceAsync(aiHand, piles, drawPile, minCards, state.MoveHistory, session);

            var validation = _engine.ValidateTurn(aiPlays, aiHand, piles, minCards);
            if (!validation.IsValid)
            {
                // AI service returned invalid plays; use local greedy
                aiPlays = GreedyFallback(aiHand, piles, minCards);
                validation = _engine.ValidateTurn(aiPlays, aiHand, piles, minCards);
                if (!validation.IsValid)
                {
                    var score = await FinalizeMultiplayerGameAsync(session, state, drawPile, "completed");
                    await _db.SaveChangesAsync();
                    return new AiTurnResult(true, "completed", score, aiMoves);
                }
            }

            var newAiHand = validation.ResultingHand.ToList();
            var newPiles = validation.ResultingPiles;

            var refillCount = drawPile.Count == 0 ? 0 : Math.Min(aiPlays.Count, drawPile.Count);
            for (var i = 0; i < refillCount; i++)
            {
                newAiHand.Add(drawPile[0]);
                drawPile.RemoveAt(0);
            }

            state.AscendingPile1 = newPiles.Ascending1;
            state.AscendingPile2 = newPiles.Ascending2;
            state.DescendingPile1 = newPiles.Descending1;
            state.DescendingPile2 = newPiles.Descending2;
            state.PlayedCardsCount += aiPlays.Count;
            state.DrawPileCards = JsonSerializer.Serialize(drawPile);
            state.UpdatedAt = DateTime.UtcNow;

            currentPlayer.Hand!.Cards = JsonSerializer.Serialize(newAiHand);
            currentPlayer.Hand.UpdatedAt = DateTime.UtcNow;

            var aiUsername = currentPlayer.User?.Username ?? "AI";
            state.MoveHistory.Add(new MoveHistoryEntry
            {
                PlayerUsername = aiUsername,
                Plays = aiPlays.Select(p => new MoveHistoryPlay { Card = p.Card, PileSlot = (int)p.Slot }).ToList()
            });

            aiMoves.Add(new LastMove(aiUsername,
                aiPlays.Select(p => new LastMovePlay(p.Card, (int)p.Slot)).ToList()));

            bool perfectGame = newAiHand.Count == 0 && drawPile.Count == 0
                && activePlayers.Where(p => p.UserId != currentPlayer.UserId)
                    .All(p => (JsonSerializer.Deserialize<List<int>>(p.Hand?.Cards ?? "[]") ?? []).Count == 0);

            if (perfectGame)
            {
                var score = await FinalizeMultiplayerGameAsync(session, state, drawPile, "completed");
                await _db.SaveChangesAsync();
                return new AiTurnResult(true, "completed", score, aiMoves);
            }

            // Advance to next player
            var currentIdx = activePlayers.FindIndex(p => p.UserId == currentPlayer.UserId);
            var nextPlayer = activePlayers[(currentIdx + 1) % activePlayers.Count];
            state.CurrentPlayerId = nextPlayer.UserId;

            var nextHand = nextPlayer.Hand is not null
                ? JsonSerializer.Deserialize<List<int>>(nextPlayer.Hand.Cards) ?? []
                : [];
            var updatedPiles = new PileTops(state.AscendingPile1, state.AscendingPile2, state.DescendingPile1, state.DescendingPile2);
            var nextMin = GameRules.GetMinCardsPerTurn(drawPileEmpty: drawPile.Count == 0, session.IsExpertMode, session.MaxPlayers);

            if (!_engine.CanPlayMinimumCards(nextHand, updatedPiles, nextMin))
            {
                var score = await FinalizeMultiplayerGameAsync(session, state, drawPile, "completed");
                await _db.SaveChangesAsync();
                return new AiTurnResult(true, "completed", score, aiMoves);
            }

            if (!nextPlayer.IsAI)
                state.CurrentTurnStartedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (!nextPlayer.IsAI)
                return new AiTurnResult(false, null, null, aiMoves);
        }
    }

    private async Task<List<CardPlay>> CallAiServiceAsync(
        IList<int> hand, PileTops piles, List<int> drawPile, int minCards,
        List<MoveHistoryEntry> moveHistory, GameSession session)
    {
        try
        {
            // playedCards = all card values not in any hand and not in draw pile
            var inPlay = session.Players
                .Where(p => !p.IsSpectator)
                .SelectMany(p => p.Hand is not null
                    ? (IEnumerable<int>)(JsonSerializer.Deserialize<List<int>>(p.Hand.Cards) ?? [])
                    : Array.Empty<int>())
                .Union(drawPile)
                .ToHashSet();

            var playedCards = Enumerable.Range(CardDeck.MinCardValue, CardDeck.TotalCards)
                .Where(c => !inPlay.Contains(c))
                .ToList();

            var requestBody = new
            {
                hand,
                piles = new
                {
                    ascending1 = piles.Ascending1,
                    ascending2 = piles.Ascending2,
                    descending1 = piles.Descending1,
                    descending2 = piles.Descending2
                },
                drawPileCount = drawPile.Count,
                minCardsThisTurn = minCards,
                playedCards,
                moveHistory = moveHistory.Select(m => new
                {
                    playerUsername = m.PlayerUsername,
                    plays = m.Plays.Select(p => new { card = p.Card, pileSlot = p.PileSlot })
                })
            };

            var json = JsonSerializer.Serialize(requestBody, _camelCase);
            var client = _httpClientFactory.CreateClient("AiService");
            var response = await client.PostAsync("/ai-move",
                new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var aiResponse = JsonSerializer.Deserialize<AiServiceResponse>(responseJson, _camelCase);

            if (aiResponse?.Plays is { Count: > 0 })
                return aiResponse.Plays.Select(p => new CardPlay(p.Card, (PileSlot)p.PileSlot)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service call failed, using local greedy fallback");
        }

        return GreedyFallback(hand, piles, minCards);
    }

    private static List<CardPlay> GreedyFallback(IList<int> hand, PileTops piles, int minCards)
    {
        var plays = new List<CardPlay>();
        var remaining = hand.ToList();
        var currentPiles = piles;

        while (plays.Count < minCards && remaining.Count > 0)
        {
            CardPlay? best = null;
            int bestDelta = int.MaxValue;

            foreach (var card in remaining)
            {
                foreach (var slot in Enum.GetValues<PileSlot>())
                {
                    var top = currentPiles.GetTop(slot);
                    bool isAsc = slot.Direction() == PileDirection.Ascending;
                    bool isBackwards = isAsc
                        ? card == top - GameRules.BackwardsTrickDelta
                        : card == top + GameRules.BackwardsTrickDelta;
                    bool isForward = isAsc ? card > top : card < top;

                    if (!isBackwards && !isForward) continue;

                    int delta = isBackwards ? 0 : Math.Abs(card - top);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        best = new CardPlay(card, slot);
                    }
                }
            }

            if (best is null) break;
            plays.Add(best);
            remaining.Remove(best.Card);
            currentPiles = currentPiles.With(best.Slot, best.Card);
        }

        return plays;
    }

    private async Task<GameScore> FinalizeMultiplayerGameAsync(
        GameSession session, GameState state, List<int> drawPile, string endReason)
    {
        state.UndoSnapshotJson = null;
        session.GamePhase = "ended";
        session.EndedAt = DateTime.UtcNow;

        var durationMinutes = session.StartedAt is null ? (int?)null
            : (int)Math.Round((session.EndedAt!.Value - session.StartedAt.Value).TotalMinutes);

        var totalCardsRemaining = session.Players
            .Where(p => !p.IsSpectator)
            .Sum(p => (JsonSerializer.Deserialize<List<int>>(p.Hand?.Cards ?? "[]") ?? []).Count)
            + drawPile.Count;

        var finalScore = _engine.CalculateScore(totalCardsRemaining);

        var result = new GameResult
        {
            GameSessionId = session.Id,
            TotalCardsRemaining = totalCardsRemaining,
            IsPerfectGame = finalScore.IsPerfectGame,
            GameDurationMinutes = durationMinutes,
            EndReason = endReason
        };
        _db.GameResults.Add(result);

        foreach (var mp in session.Players.Where(p => !p.IsAI && !p.IsSpectator))
        {
            var mpHandCount = (JsonSerializer.Deserialize<List<int>>(mp.Hand?.Cards ?? "[]") ?? []).Count;
            _db.PlayerGameStats.Add(new PlayerGameStat
            {
                GameResultId = result.Id,
                UserId = mp.UserId,
                CardsInHand = mpHandCount,
                PlayTimeMinutes = durationMinutes,
                WasReplacedByAI = mp.ReplacedByAI
            });
            // Only count stats for players who stayed until the end
            if (mp.DisconnectedAt == null)
                await UpdatePlayerStatisticsAsync(mp.UserId, finalScore, durationMinutes);
        }

        return finalScore;
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
                    Username: p.User?.Username ?? "AI",
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
        IList<LastMove>? recentMoves = null)
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
            RecentMoves: recentMoves);
    }

    private static IList<LastMove> ComputeRecentMoves(List<MoveHistoryEntry> history, string? requestingUsername)
    {
        if (history.Count == 0) return [];

        int lastOwnIndex = -1;
        if (!string.IsNullOrEmpty(requestingUsername))
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].PlayerUsername == requestingUsername)
                {
                    lastOwnIndex = i;
                    break;
                }
            }
        }

        return history
            .Skip(lastOwnIndex + 1)
            .Select(m => new LastMove(
                m.PlayerUsername,
                m.Plays.Select(p => new LastMovePlay(p.Card, p.PileSlot)).ToList()))
            .ToList();
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
