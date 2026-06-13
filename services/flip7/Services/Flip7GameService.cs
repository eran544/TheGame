using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Flip7Server.Data;
using Flip7Server.DTOs;
using Flip7Server.Game;
using Flip7Server.Models;

namespace Flip7Server.Services;

public class Flip7GameService : IFlip7GameService
{
    private readonly Flip7DbContext _db;
    private readonly IFlip7DeckShuffler _shuffler;
    private readonly IFlip7AiClient _ai;

    private const int AiTurnSafetyLimit = 1000;
    private static readonly JsonSerializerOptions JsonOptions = new();

    public Flip7GameService(Flip7DbContext db, IFlip7DeckShuffler shuffler, IFlip7AiClient ai)
    {
        _db = db;
        _shuffler = shuffler;
        _ai = ai;
    }

    public async Task<Flip7GameStateDto> CreateSoloAsync(Guid userId, string username, int? targetScore, CancellationToken ct = default)
    {
        var session = new Flip7GameSession
        {
            Id = Guid.NewGuid(),
            Mode = Flip7GameMode.Solo,
            Status = Flip7GameStatus.InProgress,
            TargetScore = targetScore is > 0 ? targetScore.Value : Flip7GameRules.DefaultTargetScore,
            RoundNumber = 0,
            DealerSeat = 0,
            CreatedBy = userId,
        };
        session.Players.Add(new Flip7Player
        {
            Id = Guid.NewGuid(),
            GameSessionId = session.Id,
            UserId = userId,
            Username = username,
            Seat = 0,
            IsAi = false,
        });

        var events = new List<Flip7Event>();
        var round = StartRound(session, events);
        await FinishSetupAsync(session, round, events, ct); // no AI in solo; banks if a Freeze on the deal ended round 1

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return Map(session, round, events);
    }

    public async Task<Flip7GameStateDto> CreateGameAsync(Flip7GameMode mode, Guid creatorUserId, string creatorUsername,
        IReadOnlyList<Flip7AiSpec> aiPlayers, int? targetScore, CancellationToken ct = default)
    {
        if (mode == Flip7GameMode.Solo)
            throw new InvalidOperationException("Use CreateSoloAsync for solo games.");

        var session = new Flip7GameSession
        {
            Id = Guid.NewGuid(),
            Mode = mode,
            Status = Flip7GameStatus.Lobby,
            TargetScore = targetScore is > 0 ? targetScore.Value : Flip7GameRules.DefaultTargetScore,
            RoundNumber = 0,
            DealerSeat = 0,
            CreatedBy = creatorUserId,
        };

        int seat = 0;
        session.Players.Add(new Flip7Player
        {
            Id = Guid.NewGuid(),
            GameSessionId = session.Id,
            UserId = creatorUserId,
            Username = creatorUsername,
            Seat = seat++,
            IsAi = false,
        });

        foreach (var spec in aiPlayers)
        {
            var style = NormalizeStyle(spec.Style);
            var difficulty = NormalizeDifficulty(spec.Difficulty);
            session.Players.Add(new Flip7Player
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                UserId = Guid.NewGuid(), // ephemeral per-game AI identity
                Username = spec.Username is { Length: > 0 } u ? u : $"AI ({style}/{difficulty})",
                Seat = seat++,
                IsAi = true,
                AiStyle = style,
                AiDifficulty = difficulty,
            });
        }

        if (mode == Flip7GameMode.VsAi)
        {
            if (!session.Players.Any(p => p.IsAi))
                throw new InvalidOperationException("A vs-AI game needs at least one AI opponent.");
            session.Status = Flip7GameStatus.InProgress;
            var events = new List<Flip7Event>();
            var round = StartRound(session, events);
            await FinishSetupAsync(session, round, events, ct);
            _db.GameSessions.Add(session);
            await _db.SaveChangesAsync(ct);
            return Map(session, round, events);
        }

        // Online: stay in the lobby until the creator starts.
        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return Map(session, round: null);
    }

    public async Task<Flip7GameStateDto> JoinAsync(Guid gameId, Guid userId, string username, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        if (session.Mode != Flip7GameMode.Online)
            throw new InvalidOperationException("Only online games accept joins.");
        if (session.Status != Flip7GameStatus.Lobby)
            throw new InvalidOperationException("This game has already started.");

        if (session.Players.Any(p => !p.IsAi && p.UserId == userId))
            return Map(session, round: null); // idempotent re-join

        // Append the human at the end and renumber seats so AI stay last is not required;
        // seat order is simply join order.
        int nextSeat = session.Players.Count;
        _db.Players.Add(new Flip7Player
        {
            Id = Guid.NewGuid(),
            GameSessionId = session.Id,
            UserId = userId,
            Username = username,
            Seat = nextSeat,
            IsAi = false,
        });
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (await GetStateAsync(gameId, userId, ct))!;
    }

    public async Task<Flip7GameStateDto> StartAsync(Guid gameId, Guid creatorUserId, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        if (session.CreatedBy != creatorUserId)
            throw new UnauthorizedAccessException("Only the creator can start the game.");
        if (session.Status != Flip7GameStatus.Lobby)
            throw new InvalidOperationException("Game has already started.");
        if (session.Players.Count < 2)
            throw new InvalidOperationException("Need at least two players to start.");

        session.Status = Flip7GameStatus.InProgress;
        var events = new List<Flip7Event>();
        var round = StartRound(session, events);
        await FinishSetupAsync(session, round, events, ct);
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(session, round, events);
    }

    public Task<Flip7GameStateDto> HitAsync(Guid gameId, Guid userId, CancellationToken ct = default) =>
        ApplyTurnAsync(gameId, userId, hit: true, ct);

    public Task<Flip7GameStateDto> StayAsync(Guid gameId, Guid userId, CancellationToken ct = default) =>
        ApplyTurnAsync(gameId, userId, hit: false, ct);

    public async Task<Flip7GameStateDto> ChooseTargetAsync(Guid gameId, Guid userId, Guid targetPlayerId, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        EnsureParticipant(session, userId);

        if (session.Status != Flip7GameStatus.InProgress)
            throw new InvalidOperationException("Game is not in progress.");

        var round = RestoreRound(session) ?? throw new InvalidOperationException("No active round.");
        if (round.PendingAction is null)
            throw new InvalidOperationException("No action card is awaiting a target.");

        var player = ResolveActingPlayer(session, userId);
        if (round.PendingAction.DrawerId != player.Id)
            throw new InvalidOperationException("Only the player who drew the action card chooses its target.");

        var chooser = ChooserFor(session, round);
        var events = new List<Flip7Event>();
        events.AddRange(round.ResolveTarget(player.Id, targetPlayerId, chooser));

        await DriveAiTurnsAsync(session, round, chooser, events, ct);

        if (round.RoundEnded)
            BankRound(session, round);

        session.RoundStateJson = Serialize(round.Capture());
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(session, round, events);
    }

    public async Task<Flip7GameStateDto> NextRoundAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        EnsureParticipant(session, userId);

        if (session.Status != Flip7GameStatus.InProgress)
            throw new InvalidOperationException("Game is not in progress.");

        var current = RestoreRound(session);
        if (current is { RoundEnded: false })
            throw new InvalidOperationException("The current round is still in progress.");

        session.DealerSeat = Flip7GameRules.NextDealerSeat(session.DealerSeat, session.Players.Count);
        var events = new List<Flip7Event>();
        var round = StartRound(session, events);
        await FinishSetupAsync(session, round, events, ct);

        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(session, round, events);
    }

    public async Task<Flip7GameStateDto?> GetStateAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct);
        if (session is null) return null;
        EnsureParticipant(session, userId);
        return Map(session, RestoreRound(session)); // plain read: no events
    }

    // ---- Core turn application ------------------------------------------

    private async Task<Flip7GameStateDto> ApplyTurnAsync(Guid gameId, Guid userId, bool hit, CancellationToken ct)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        EnsureParticipant(session, userId);

        if (session.Status != Flip7GameStatus.InProgress)
            throw new InvalidOperationException("Game is not in progress.");

        var round = RestoreRound(session) ?? throw new InvalidOperationException("No active round.");
        if (round.RoundEnded)
            throw new InvalidOperationException("The round has ended; deal the next round.");
        if (round.PendingAction is not null)
            throw new InvalidOperationException("Choose a target for the drawn action card first.");

        var player = ResolveActingPlayer(session, userId);
        if (round.CurrentPlayerId != player.Id)
            throw new InvalidOperationException("It is not your turn.");

        var chooser = ChooserFor(session, round);
        var events = new List<Flip7Event>();
        if (hit) events.AddRange(round.Hit(player.Id, chooser));
        else events.AddRange(round.Stay(player.Id));

        await DriveAiTurnsAsync(session, round, chooser, events, ct);

        if (round.RoundEnded)
            BankRound(session, round);

        session.RoundStateJson = Serialize(round.Capture());
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(session, round, events);
    }

    // ---- Round lifecycle ------------------------------------------------

    private Flip7Round StartRound(Flip7GameSession session, List<Flip7Event> events)
    {
        var bySeat = session.Players.OrderBy(p => p.Seat).Select(p => p.Id).ToList();
        var turnOrder = Flip7GameRules.TurnOrderFromDealer(bySeat, session.DealerSeat);
        var deck = _shuffler.CreateShuffledDeck();

        var round = new Flip7Round(turnOrder, deck);
        session.RoundNumber += 1;
        // AI draws on the deal auto-target; a human drawing an action pauses for a pick.
        events.AddRange(round.DealInitial(ChooserFor(session, round)));
        return round;
    }

    /// <summary>After a round is dealt/advanced: let AI players act, bank if it ended, then snapshot.</summary>
    private async Task FinishSetupAsync(Flip7GameSession session, Flip7Round round, List<Flip7Event> events, CancellationToken ct)
    {
        await DriveAiTurnsAsync(session, round, ChooserFor(session, round), events, ct);
        if (round.RoundEnded)
            BankRound(session, round);
        session.RoundStateJson = Serialize(round.Capture());
    }

    /// <summary>
    /// Plays out consecutive AI turns until a human is on turn, the round ends,
    /// or a human's choice is pending. AI drawers auto-target, so AI play never
    /// suspends.
    /// </summary>
    private async Task DriveAiTurnsAsync(Flip7GameSession session, Flip7Round round, TargetChooser chooser, List<Flip7Event> events, CancellationToken ct)
    {
        int guard = 0;
        while (!round.RoundEnded && round.PendingAction is null && round.CurrentPlayerId is Guid current)
        {
            var player = session.Players.First(p => p.Id == current);
            if (!player.IsAi) break;
            if (++guard > AiTurnSafetyLimit) break;

            var line = round.Line(player.Id);
            bool canStay = line.Numbers.Count > 0 || line.Modifiers.Count > 0;

            var action = await _ai.DecideHitOrStayAsync(BuildAiRequest(session, round, player), ct);
            if (action == "stay" && canStay)
                events.AddRange(round.Stay(player.Id));
            else
                events.AddRange(round.Hit(player.Id, chooser));
        }
    }

    private static Flip7AiMoveRequest BuildAiRequest(Flip7GameSession session, Flip7Round round, Flip7Player me)
    {
        var line = round.Line(me.Id);
        var opponents = session.Players
            .Where(p => p.Id != me.Id)
            .Select(p =>
            {
                var l = round.Line(p.Id);
                return new Flip7AiOpponent
                {
                    NumberCount = l.Numbers.Count,
                    RoundScore = l.Score,
                    Status = l.Status.ToString().ToLowerInvariant(),
                    CumulativeScore = p.CumulativeScore,
                };
            })
            .ToList();

        return new Flip7AiMoveRequest
        {
            MyNumbers = line.Numbers.ToList(),
            MyModifiers = line.Modifiers.Select(m => m.ToString()).ToList(),
            HasSecondChance = line.HasSecondChance,
            MyRoundScore = line.Score,
            MyCumulativeScore = me.CumulativeScore,
            TargetScore = session.TargetScore,
            DeckRemaining = round.DrawableNumberCounts(),
            DrawPileCount = round.DrawableCount,
            Opponents = opponents,
            Style = me.AiStyle ?? "balanced",
            Difficulty = me.AiDifficulty ?? "medium",
        };
    }

    /// <summary>
    /// AI drawers auto-target via the heuristic selector; human drawers return
    /// null so the round suspends and the player picks interactively — even when
    /// they are the only legal target (they still confirm it).
    /// </summary>
    private static TargetChooser ChooserFor(Flip7GameSession session, Flip7Round round) =>
        (action, drawerId, candidates) =>
        {
            var drawer = session.Players.FirstOrDefault(p => p.Id == drawerId);
            return drawer is { IsAi: true }
                ? Flip7TargetSelector.Choose(action, drawerId, candidates, round)
                : (Guid?)null;
        };

    private static string NormalizeStyle(string? s) =>
        s?.ToLowerInvariant() is "safe" or "balanced" or "risky" ? s!.ToLowerInvariant() : "balanced";

    private static string NormalizeDifficulty(string? d) =>
        d?.ToLowerInvariant() is "easy" or "medium" or "hard" ? d!.ToLowerInvariant() : "medium";

    private void BankRound(Flip7GameSession session, Flip7Round round)
    {
        foreach (var player in session.Players)
        {
            int score = round.Scores.TryGetValue(player.Id, out var s) ? s : 0;
            player.CumulativeScore += score;

            var line = round.Line(player.Id);
            _db.RoundResults.Add(new Flip7RoundResult
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                RoundNumber = session.RoundNumber,
                PlayerId = player.Id,
                RoundScore = score,
                Busted = line.Status == PlayerLineStatus.Busted,
                AchievedFlip7 = line.AchievedFlip7,
                CumulativeAfter = player.CumulativeScore,
            });
        }

        if (Flip7GameRules.IsGameOver(session.Players.Select(p => p.CumulativeScore), session.TargetScore))
        {
            session.Status = Flip7GameStatus.Completed;
            session.WinnerId = session.Players
                .OrderByDescending(p => p.CumulativeScore)
                .ThenBy(p => p.Seat)
                .First().Id;
        }
    }

    // ---- Persistence & mapping ------------------------------------------

    private Task<Flip7GameSession?> LoadAsync(Guid gameId, CancellationToken ct) =>
        _db.GameSessions
            .Include(g => g.Players)
            .Include(g => g.RoundResults)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

    private Flip7Round? RestoreRound(Flip7GameSession session) =>
        session.RoundStateJson is null ? null : Flip7Round.Restore(Deserialize(session.RoundStateJson));

    private Flip7Player ResolveActingPlayer(Flip7GameSession session, Guid userId)
    {
        var player = session.Players.FirstOrDefault(p => !p.IsAi && p.UserId == userId)
            ?? throw new InvalidOperationException("You are not a player in this game.");
        return player;
    }

    private static void EnsureParticipant(Flip7GameSession session, Guid userId)
    {
        if (!session.Players.Any(p => !p.IsAi && p.UserId == userId))
            throw new UnauthorizedAccessException("You are not a player in this game.");
    }

    private static string Serialize(Flip7RoundSnapshot snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);

    private static Flip7RoundSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<Flip7RoundSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("Corrupt round state.");

    private static Flip7GameStateDto Map(Flip7GameSession session, Flip7Round? round, IReadOnlyList<Flip7Event>? events = null)
    {
        var players = session.Players
            .OrderBy(p => p.Seat)
            .Select(p => MapPlayer(p, round))
            .ToList();

        Flip7PendingActionDto? pending = round?.PendingAction is { } pa
            ? new Flip7PendingActionDto
            {
                Action = pa.Action.ToString(),
                DrawerId = pa.DrawerId,
                CandidateIds = pa.Candidates.ToList(),
            }
            : null;

        var eventDtos = (events ?? Array.Empty<Flip7Event>())
            .Select(MapEvent)
            .ToList();

        return new Flip7GameStateDto
        {
            Id = session.Id,
            Mode = session.Mode.ToString(),
            Status = session.Status.ToString(),
            TargetScore = session.TargetScore,
            RoundNumber = session.RoundNumber,
            DealerSeat = session.DealerSeat,
            CurrentPlayerId = round?.CurrentPlayerId,
            RoundEnded = round?.RoundEnded ?? false,
            RoundEndReason = (round?.EndReason ?? RoundEndReason.None).ToString(),
            WinnerId = session.WinnerId,
            Players = players,
            PendingAction = pending,
            Events = eventDtos,
            ActionId = eventDtos.Count > 0 ? Guid.NewGuid() : null,
        };
    }

    private static Flip7EventDto MapEvent(Flip7Event e) => new()
    {
        Type = e.Type.ToString(),
        PlayerId = e.PlayerId,
        SourcePlayerId = e.SourcePlayerId,
        Card = e.Card?.ToString(),
        Detail = e.Detail,
    };

    private static Flip7PlayerStateDto MapPlayer(Flip7Player p, Flip7Round? round)
    {
        var line = round is not null && round.Lines.TryGetValue(p.Id, out var l) ? l : null;

        return new Flip7PlayerStateDto
        {
            Id = p.Id,
            UserId = p.UserId,
            Username = p.Username,
            Seat = p.Seat,
            IsAi = p.IsAi,
            AiStyle = p.AiStyle,
            AiDifficulty = p.AiDifficulty,
            CumulativeScore = p.CumulativeScore,
            Numbers = line?.Numbers.ToList() ?? new List<int>(),
            Modifiers = line?.Modifiers.Select(m => m.ToString()).ToList() ?? new List<string>(),
            HasSecondChance = line?.HasSecondChance ?? false,
            Status = (line?.Status ?? PlayerLineStatus.Active).ToString(),
            AchievedFlip7 = line?.AchievedFlip7 ?? false,
            RoundScore = line?.Score ?? 0,
        };
    }
}
