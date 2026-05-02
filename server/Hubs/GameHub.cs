using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TheGameServer.Services.Game;

namespace TheGameServer.Hubs;

[Authorize]
public class GameHub : Hub
{
    // Maps connectionId → (sessionId, userId) so OnDisconnectedAsync can clean up.
    private static readonly ConcurrentDictionary<string, (Guid SessionId, Guid UserId)> _connections = new();

    private readonly IGameService _gameService;

    public GameHub(IGameService gameService)
    {
        _gameService = gameService;
    }

    public async Task JoinGame(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        var userId = GetUserId();
        if (userId != Guid.Empty && Guid.TryParse(sessionId, out var sid))
            _connections[Context.ConnectionId] = (sid, userId);
    }

    public async Task LeaveGame(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));
        _connections.TryRemove(Context.ConnectionId, out _);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var info))
        {
            var result = await _gameService.LeaveGameAsync(info.SessionId, info.UserId);
            if (result.Success && result.Value!.GameEnded)
            {
                await Clients.Group(GroupName(info.SessionId.ToString()))
                    .SendAsync("GameEnded", new { reason = "disconnection" });
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(string sessionId) => $"game:{sessionId}";

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    // Server broadcasts to group (sent from GameController via IHubContext or directly):
    //   "LobbyUpdated"    → LobbyStateDto   (player joined/lobby changed)
    //   "PlayerLeft"      → Guid userId      (player left lobby or game)
    //   "GameStarted"     → GameStateDto     (lobby → playing)
    //   "GameStateUpdated"→ GameStateDto     (after each turn / undo)
    //   "GameEnded"       → GameStateDto | { reason }  (game over)
}
