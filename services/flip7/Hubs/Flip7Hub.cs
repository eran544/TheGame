using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Flip7Server.DTOs;
using Flip7Server.Services;

namespace Flip7Server.Hubs;

/// <summary>
/// Serializes all state-changing work for a given game across hub invocations.
/// A live action can stream several AI beats (with pacing delays) before it
/// returns; this guard keeps a second caller from interleaving and double-applying
/// turns against the same round. One semaphore per game, kept for the app's life.
/// </summary>
internal static class Flip7GameLocks
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();
    public static SemaphoreSlim For(Guid gameId) => Locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
}

/// <summary>
/// Real-time hub for Flip 7 games (one SignalR group per game session). Clients
/// call <see cref="JoinGame"/> to subscribe, then <see cref="Hit"/>/<see cref="Stay"/>/
/// <see cref="Start"/>/<see cref="NextRound"/>; every state-changing action
/// broadcasts the new <see cref="Flip7GameStateDto"/> to the whole group so all
/// players (and spectators) stay in sync. Errors are sent only to the caller.
/// </summary>
[Authorize]
public class Flip7Hub : Hub
{
    private readonly IFlip7GameService _games;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<Flip7Hub> _hubContext;
    private readonly ILogger<Flip7Hub> _logger;

    /// <summary>Delay between successive (AI) beats so play reads like a real opponent's turn.</summary>
    private const int AiStepDelayMs = 850;

    public Flip7Hub(
        IFlip7GameService games,
        IServiceScopeFactory scopeFactory,
        IHubContext<Flip7Hub> hubContext,
        ILogger<Flip7Hub> logger)
    {
        _games = games;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public static string GroupName(string gameId) => $"flip7:{gameId}";

    private Guid UserId =>
        Guid.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    private string Username => Context.User?.FindFirst("username")?.Value ?? "player";

    /// <summary>
    /// Subscribe to a game's updates and receive the current state. If an AI is on
    /// turn (a vs-AI game whose opening turns haven't run yet), play those out now —
    /// paced and broadcast to the group — so they animate for whoever is watching.
    /// </summary>
    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
        if (!Guid.TryParse(gameId, out var id))
            return;

        var state = await _games.GetStateAsync(id, UserId);
        if (state is null)
            return;
        await Clients.Caller.SendAsync("GameState", state);

        // Animate any opening AI turns. This must NOT run on the caller's connection:
        // React StrictMode (and reconnects) tear the first connection down moments after
        // it joins, which would dispose this invocation's DI scope mid-drive and leave a
        // half-applied round for the survivor to re-drive. So the drive runs detached,
        // with its own scope and IHubContext, broadcasting to the group. See DriveOpeningAiTurns.
        DriveOpeningAiTurns(gameId, id);
    }

    /// <summary>
    /// Plays out a vs-AI game's opening AI turns (round 1, where the human deals and
    /// acts last) on a background task, paced and broadcast to the whole group. Decoupled
    /// from any one connection so it survives the caller disconnecting mid-animation.
    /// A non-blocking lock acquire means a doomed first mount that wins the race drives
    /// the animation, while the surviving mount's attempt simply no-ops instead of
    /// triggering a second, overlapping drive against the same round.
    /// </summary>
    private void DriveOpeningAiTurns(string gameId, Guid id)
    {
        _ = Task.Run(async () =>
        {
            var gate = Flip7GameLocks.For(id);
            if (!await gate.WaitAsync(0))
                return; // another action or drive already holds this game
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var games = scope.ServiceProvider.GetRequiredService<IFlip7GameService>();
                var group = _hubContext.Clients.Group(GroupName(gameId));
                AiStepCallback onUpdate = async beat =>
                {
                    await Task.Delay(AiStepDelayMs); // a beat before/between the AI's turns
                    await group.SendAsync("GameState", beat);
                };
                await games.DriveAiAsync(id, onUpdate);
            }
            catch (Exception ex)
            {
                // Best-effort animation from a detached task; the authoritative state is
                // persisted regardless, and the next action will reconcile clients.
                _logger.LogWarning(ex, "Opening AI drive failed for game {GameId}", id);
            }
            finally { gate.Release(); }
        });
    }

    public Task LeaveGame(string gameId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));

    /// <summary>Online lobby: add this user as a player, then broadcast the lobby.</summary>
    public Task JoinLobby(string gameId) =>
        Act(gameId, (id, _) => _games.JoinAsync(id, UserId, Username));

    public Task Start(string gameId) =>
        Act(gameId, (id, onUpdate) => _games.StartAsync(id, UserId, onUpdate));

    public Task Hit(string gameId) =>
        Act(gameId, (id, onUpdate) => _games.HitAsync(id, UserId, onUpdate));

    public Task Stay(string gameId) =>
        Act(gameId, (id, onUpdate) => _games.StayAsync(id, UserId, onUpdate));

    public Task ChooseTarget(string gameId, string targetPlayerId) =>
        Act(gameId, (id, onUpdate) =>
        {
            if (!Guid.TryParse(targetPlayerId, out var target))
                throw new InvalidOperationException("Invalid target.");
            return _games.ChooseTargetAsync(id, UserId, target, onUpdate);
        });

    public Task NextRound(string gameId) =>
        Act(gameId, (id, onUpdate) => _games.NextRoundAsync(id, UserId, onUpdate));

    /// <summary>
    /// Runs one game action, serialized per game. The action may stream several
    /// beats (its own result, then each AI turn) through <c>onUpdate</c>; the first
    /// is broadcast immediately and each later one after a short delay so the AI
    /// appears to take real turns. Actions that don't stream (lobby joins) fall back
    /// to broadcasting their single return value.
    /// </summary>
    private async Task Act(string gameId, Func<Guid, AiStepCallback, Task<Flip7GameStateDto>> action)
    {
        if (!Guid.TryParse(gameId, out var id))
        {
            await Clients.Caller.SendAsync("Error", "Invalid game id.");
            return;
        }

        var group = Clients.Group(GroupName(gameId));
        var streamed = false;
        AiStepCallback onUpdate = async state =>
        {
            if (streamed) await Task.Delay(AiStepDelayMs); // pace beats after the first
            streamed = true;
            await group.SendAsync("GameState", state);
        };

        var gate = Flip7GameLocks.For(id);
        await gate.WaitAsync();
        try
        {
            var state = await action(id, onUpdate);
            if (!streamed)
                await group.SendAsync("GameState", state);
        }
        catch (KeyNotFoundException) { await Clients.Caller.SendAsync("Error", "Game not found."); }
        catch (UnauthorizedAccessException ex) { await Clients.Caller.SendAsync("Error", ex.Message); }
        catch (InvalidOperationException ex) { await Clients.Caller.SendAsync("Error", ex.Message); }
        finally { gate.Release(); }
    }
}
