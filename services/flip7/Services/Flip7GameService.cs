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

    private static readonly JsonSerializerOptions JsonOptions = new();

    public Flip7GameService(Flip7DbContext db, IFlip7DeckShuffler shuffler)
    {
        _db = db;
        _shuffler = shuffler;
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

        var round = StartRound(session);
        BankIfRoundEnded(session, round); // defensive: a Freeze on the solo deal could end round 1 instantly

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return Map(session, round);
    }

    public Task<Flip7GameStateDto> HitAsync(Guid gameId, Guid userId, CancellationToken ct = default) =>
        ApplyTurnAsync(gameId, userId, hit: true, ct);

    public Task<Flip7GameStateDto> StayAsync(Guid gameId, Guid userId, CancellationToken ct = default) =>
        ApplyTurnAsync(gameId, userId, hit: false, ct);

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
        var round = StartRound(session);
        BankIfRoundEnded(session, round);

        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(session, round);
    }

    public async Task<Flip7GameStateDto?> GetStateAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var session = await LoadAsync(gameId, ct);
        if (session is null) return null;
        EnsureParticipant(session, userId);
        return Map(session, RestoreRound(session));
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

        var player = ResolveActingPlayer(session, userId);
        if (round.CurrentPlayerId != player.Id)
            throw new InvalidOperationException("It is not your turn.");

        if (hit) round.Hit(player.Id);
        else round.Stay(player.Id);

        if (round.RoundEnded)
            BankRound(session, round);

        session.RoundStateJson = Serialize(round.Capture());
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(session, round);
    }

    // ---- Round lifecycle ------------------------------------------------

    private Flip7Round StartRound(Flip7GameSession session)
    {
        var bySeat = session.Players.OrderBy(p => p.Seat).Select(p => p.Id).ToList();
        var turnOrder = Flip7GameRules.TurnOrderFromDealer(bySeat, session.DealerSeat);
        var deck = _shuffler.CreateShuffledDeck();

        var round = new Flip7Round(turnOrder, deck);
        round.DealInitial(); // solo: action cards self-target automatically

        session.RoundNumber += 1;
        session.RoundStateJson = Serialize(round.Capture());
        return round;
    }

    private void BankIfRoundEnded(Flip7GameSession session, Flip7Round round)
    {
        if (round.RoundEnded)
        {
            BankRound(session, round);
            session.RoundStateJson = Serialize(round.Capture());
        }
    }

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

    private static Flip7GameStateDto Map(Flip7GameSession session, Flip7Round? round)
    {
        var players = session.Players
            .OrderBy(p => p.Seat)
            .Select(p => MapPlayer(p, round))
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
        };
    }

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
