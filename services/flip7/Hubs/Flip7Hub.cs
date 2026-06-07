using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Flip7Server.DTOs;
using Flip7Server.Services;

namespace Flip7Server.Hubs;

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

    public Flip7Hub(IFlip7GameService games) => _games = games;

    public static string GroupName(string gameId) => $"flip7:{gameId}";

    private Guid UserId =>
        Guid.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    private string Username => Context.User?.FindFirst("username")?.Value ?? "player";

    /// <summary>Subscribe to a game's updates and receive the current state.</summary>
    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
        if (Guid.TryParse(gameId, out var id))
        {
            var state = await _games.GetStateAsync(id, UserId);
            if (state is not null)
                await Clients.Caller.SendAsync("GameState", state);
        }
    }

    public Task LeaveGame(string gameId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));

    /// <summary>Online lobby: add this user as a player, then broadcast the lobby.</summary>
    public Task JoinLobby(string gameId) =>
        Act(gameId, id => _games.JoinAsync(id, UserId, Username));

    public Task Start(string gameId) =>
        Act(gameId, id => _games.StartAsync(id, UserId));

    public Task Hit(string gameId) =>
        Act(gameId, id => _games.HitAsync(id, UserId));

    public Task Stay(string gameId) =>
        Act(gameId, id => _games.StayAsync(id, UserId));

    public Task NextRound(string gameId) =>
        Act(gameId, id => _games.NextRoundAsync(id, UserId));

    private async Task Act(string gameId, Func<Guid, Task<Flip7GameStateDto>> action)
    {
        if (!Guid.TryParse(gameId, out var id))
        {
            await Clients.Caller.SendAsync("Error", "Invalid game id.");
            return;
        }

        try
        {
            var state = await action(id);
            await Clients.Group(GroupName(gameId)).SendAsync("GameState", state);
        }
        catch (KeyNotFoundException) { await Clients.Caller.SendAsync("Error", "Game not found."); }
        catch (UnauthorizedAccessException ex) { await Clients.Caller.SendAsync("Error", ex.Message); }
        catch (InvalidOperationException ex) { await Clients.Caller.SendAsync("Error", ex.Message); }
    }
}
