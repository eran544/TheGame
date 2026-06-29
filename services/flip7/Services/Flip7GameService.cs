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
        SettleDeal(session, round); // no AI in solo; banks if a Freeze on the deal ended round 1

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
            // Begin the round but deal nothing yet: the hub animates the deal AND the
            // opening AI turns when the creator's client connects (DriveAiAsync), so the
            // whole of round 1 — including an action card dealt onto the human — plays out
            // live instead of being resolved before anyone is watching.
            var round = BeginRound(session);
            session.RoundStateJson = Serialize(round.Capture());
            _db.GameSessions.Add(session);
            await _db.SaveChangesAsync(ct);
            return Map(session, round); // un-dealt board; the hub deals it on connect
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

    public async Task<Flip7GameStateDto> StartAsync(Guid gameId, Guid creatorUserId, AiStepCallback? onUpdate = null, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        if (session.CreatedBy != creatorUserId)
            throw new UnauthorizedAccessException("Only the creator can start the game.");
        if (session.Status != Flip7GameStatus.Lobby)
            throw new InvalidOperationException("Game has already started.");
        if (session.Players.Count < 2)
            throw new InvalidOperationException("Need at least two players to start.");

        session.Status = Flip7GameStatus.InProgress;
        var round = BeginRound(session);
        return await DealAndEmitAsync(session, round, onUpdate, ct);
    }

    public Task<Flip7GameStateDto> HitAsync(Guid gameId, Guid userId, AiStepCallback? onUpdate = null, CancellationToken ct = default) =>
        ApplyTurnAsync(gameId, userId, hit: true, onUpdate, ct);

    public Task<Flip7GameStateDto> StayAsync(Guid gameId, Guid userId, AiStepCallback? onUpdate = null, CancellationToken ct = default) =>
        ApplyTurnAsync(gameId, userId, hit: false, onUpdate, ct);

    public async Task<Flip7GameStateDto> ChooseTargetAsync(Guid gameId, Guid userId, Guid targetPlayerId, AiStepCallback? onUpdate = null, CancellationToken ct = default)
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

        var events = new List<Flip7Event>();
        events.AddRange(round.ResolveTarget(player.Id, targetPlayerId, ChooserFor(session, round)));

        return await ResolveAndEmitAsync(session, round, events, onUpdate, ct);
    }

    public async Task<Flip7GameStateDto> NextRoundAsync(Guid gameId, Guid userId, AiStepCallback? onUpdate = null, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct) ?? throw new KeyNotFoundException("Game not found.");
        EnsureParticipant(session, userId);

        if (session.Status != Flip7GameStatus.InProgress)
            throw new InvalidOperationException("Game is not in progress.");

        var current = RestoreRound(session);
        if (current is { RoundEnded: false })
            throw new InvalidOperationException("The current round is still in progress.");

        session.DealerSeat = Flip7GameRules.NextDealerSeat(session.DealerSeat, session.Players.Count);
        var round = BeginRound(session);
        return await DealAndEmitAsync(session, round, onUpdate, ct);
    }

    public async Task<Flip7GameStateDto?> GetStateAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct);
        if (session is null) return null;
        EnsureParticipant(session, userId);
        return Map(session, RestoreRound(session)); // plain read: no events
    }

    // ---- Core turn application ------------------------------------------

    private async Task<Flip7GameStateDto> ApplyTurnAsync(Guid gameId, Guid userId, bool hit, AiStepCallback? onUpdate, CancellationToken ct)
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

        var events = new List<Flip7Event>();
        if (hit) events.AddRange(round.Hit(player.Id, ChooserFor(session, round)));
        else events.AddRange(round.Stay(player.Id));

        return await ResolveAndEmitAsync(session, round, events, onUpdate, ct);
    }

    // ---- Round lifecycle ------------------------------------------------

    /// <summary>Builds the next round (turn order + fresh deck) without dealing it.</summary>
    private Flip7Round NewRound(Flip7GameSession session)
    {
        var bySeat = session.Players.OrderBy(p => p.Seat).Select(p => p.Id).ToList();
        var turnOrder = Flip7GameRules.TurnOrderFromDealer(bySeat, session.DealerSeat);
        var deck = _shuffler.CreateShuffledDeck();
        session.RoundNumber += 1;
        return new Flip7Round(turnOrder, deck);
    }

    /// <summary>Solo / atomic path: deal the whole round now (no streaming to animate it).</summary>
    private Flip7Round StartRound(Flip7GameSession session, List<Flip7Event> events)
    {
        var round = NewRound(session);
        // AI draws on the deal auto-target; a human drawing an action pauses for a pick.
        events.AddRange(round.DealInitial(ChooserFor(session, round)));
        return round;
    }

    /// <summary>
    /// Streamed path: set up the round and begin its deal without handing out cards.
    /// The drive loop then animates the deal one seat per beat, so the whole table
    /// watches the round be dealt (including any action card dealt onto a player).
    /// </summary>
    private Flip7Round BeginRound(Flip7GameSession session)
    {
        var round = NewRound(session);
        round.BeginDeal();
        return round;
    }

    /// <summary>
    /// Finalizes a freshly dealt round at creation time (REST, no live streaming):
    /// banks if the deal alone ended the round (e.g. a Freeze on the solo deal),
    /// then snapshots. The caller saves. Opening AI turns are deliberately NOT
    /// played here — the hub animates them when a client connects (see
    /// <see cref="DriveAiAsync"/>) so players watch the AI's first turns too.
    /// </summary>
    private void SettleDeal(Flip7GameSession session, Flip7Round round)
    {
        if (round.RoundEnded)
            BankRound(session, round);
        session.RoundStateJson = Serialize(round.Capture());
    }

    /// <summary>
    /// Animates whatever the round owes that no human input gates — the remaining
    /// initial-deal seats, then any opening AI turns — emitting each beat via
    /// <paramref name="onUpdate"/>; a no-op once it is a human's turn or nothing is
    /// pending. The hub calls this when a client connects so a vs-AI game's deal and
    /// opening AI turns play out live rather than being resolved before anyone watches.
    /// </summary>
    public async Task DriveAiAsync(Guid gameId, AiStepCallback onUpdate, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct);
        if (session is null || session.Status != Flip7GameStatus.InProgress) return;

        var round = RestoreRound(session);
        if (round is null || round.RoundEnded || round.PendingAction is not null) return;

        // Drive if the deal still needs animating, or an AI is on turn. (A human already
        // on turn with the deal finished leaves nothing to do.)
        bool aiOnTurn = round.CurrentPlayerId is Guid current
            && session.Players.First(p => p.Id == current).IsAi;
        if (!round.Dealing && !aiOnTurn) return;

        var events = new List<Flip7Event>();
        await DriveAsync(session, round, ChooserFor(session, round), events, onUpdate, ct);
        await PersistAsync(session, round, ct);
    }

    /// <summary>
    /// Drives a freshly begun round: animates the initial deal one seat per beat,
    /// then the opening AI turns. With <paramref name="onUpdate"/> (the live hub
    /// path) each beat is persisted and pushed individually so the whole table
    /// watches the deal and AI act in real time; otherwise everything resolves in
    /// one shot and is saved once (REST/tests). The deal can suspend on a human's
    /// dealt-action target choice, leaving the picker up for them.
    /// </summary>
    private async Task<Flip7GameStateDto> DealAndEmitAsync(
        Flip7GameSession session, Flip7Round round, AiStepCallback? onUpdate, CancellationToken ct)
    {
        var events = new List<Flip7Event>();
        await DriveAsync(session, round, ChooserFor(session, round), events, onUpdate, ct);
        await PersistAsync(session, round, ct);
        return Map(session, round, events);
    }

    /// <summary>
    /// Settles a base action (a human turn or target choice) and the AI turns that
    /// follow it. When <paramref name="onUpdate"/> is supplied (the live hub path)
    /// each beat — the base action, then every AI turn — is persisted and pushed
    /// individually so all players watch the AI act in real time. Otherwise
    /// everything resolves in one shot and is saved once (REST/solo/tests).
    /// </summary>
    private async Task<Flip7GameStateDto> ResolveAndEmitAsync(
        Flip7GameSession session, Flip7Round round, List<Flip7Event> events,
        AiStepCallback? onUpdate, CancellationToken ct)
    {
        if (round.RoundEnded)
            BankRound(session, round); // the base action itself ended the round

        if (onUpdate is not null)
        {
            await PersistAsync(session, round, ct);
            await onUpdate(Map(session, round, events)); // base beat, shown immediately
        }

        await DriveAsync(session, round, ChooserFor(session, round), events, onUpdate, ct);

        await PersistAsync(session, round, ct);
        return Map(session, round, events);
    }

    /// <summary>
    /// Advances the round through work no human input gates: first any remaining
    /// initial-deal seats (one beat each), then consecutive AI turns. Stops at a
    /// human's turn, a pending target choice, or round end. With
    /// <paramref name="onUpdate"/> set, each seat dealt and each AI turn is persisted
    /// and emitted as its own beat (the hub paces them); otherwise it resolves in one
    /// shot for the caller to persist.
    /// </summary>
    private async Task DriveAsync(Flip7GameSession session, Flip7Round round, TargetChooser chooser,
        List<Flip7Event> events, AiStepCallback? onUpdate, CancellationToken ct)
    {
        // 1. Deal, one seat per beat.
        while (round.Dealing && round.PendingAction is null && !round.RoundEnded)
        {
            var beat = round.DealNext(chooser).ToList();
            events.AddRange(beat);
            if (round.RoundEnded)
                BankRound(session, round); // a deal action (e.g. a Freeze) ended the round

            if (onUpdate is not null)
            {
                await PersistAsync(session, round, ct);
                if (beat.Count > 0)
                    await onUpdate(Map(session, round, beat)); // this dealt seat, on its own
            }
        }

        // 2. The AI turns that follow.
        await DriveAiTurnsAsync(session, round, chooser, events, onUpdate, ct);
    }

    /// <summary>
    /// Plays out consecutive AI turns until a human is on turn, the round ends,
    /// or a human's choice is pending. AI drawers auto-target, so AI play never
    /// suspends. With <paramref name="onUpdate"/> set, each AI turn is persisted
    /// and emitted as its own update (the hub paces these with delays).
    /// </summary>
    private async Task DriveAiTurnsAsync(Flip7GameSession session, Flip7Round round, TargetChooser chooser,
        List<Flip7Event> events, AiStepCallback? onUpdate, CancellationToken ct)
    {
        int guard = 0;
        while (!round.RoundEnded && round.PendingAction is null && round.CurrentPlayerId is Guid current)
        {
            var player = session.Players.First(p => p.Id == current);
            if (!player.IsAi) break;
            if (++guard > AiTurnSafetyLimit) break;

            var beat = new List<Flip7Event>();
            var line = round.Line(player.Id);
            bool canStay = line.Numbers.Count > 0 || line.Modifiers.Count > 0;

            var action = await _ai.DecideHitOrStayAsync(BuildAiRequest(session, round, player), ct);
            if (action == "stay" && canStay)
                beat.AddRange(round.Stay(player.Id));
            else
                beat.AddRange(round.Hit(player.Id, chooser));

            events.AddRange(beat);
            if (round.RoundEnded)
                BankRound(session, round); // this AI turn ended the round

            if (onUpdate is not null)
            {
                await PersistAsync(session, round, ct);
                await onUpdate(Map(session, round, beat)); // this AI turn, on its own
            }
        }
    }

    private async Task PersistAsync(Flip7GameSession session, Flip7Round round, CancellationToken ct)
    {
        session.RoundStateJson = Serialize(round.Capture());
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
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
            BustedNumber = line?.BustedNumber,
            RoundScore = line?.Score ?? 0,
        };
    }
}
