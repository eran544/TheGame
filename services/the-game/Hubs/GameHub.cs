using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TheGameServer.Mappers;
using TheGameServer.Services.Game;

namespace TheGameServer.Hubs;

[Authorize]
public class GameHub : Hub
{
    // Maps connectionId → (sessionId, userId) so OnDisconnectedAsync can clean up.
    private static readonly ConcurrentDictionary<string, (Guid SessionId, Guid UserId)> _connections = new();

    // Delay between streamed AI catch-up turns so a takeover reads like real turns.
    private const int TurnBeatDelayMs = 800;

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
            var group = Clients.Group(GroupName(info.SessionId.ToString()));

            // Announce the AI takeover, then stream each of the AI's catch-up turns,
            // paced like real turns, so the remaining players watch play unfold in order.
            // This awaits inside OnDisconnectedAsync (whose scope and Clients stay valid
            // for the duration); the beats broadcast to the group, not the gone caller.
            var beats = 0;
            Func<string, string, Task> onReplaced = (disconnected, ai) =>
                group.SendAsync("PlayerReplacedByAI", new { disconnectedUsername = disconnected, aiUsername = ai });
            Func<GameStateView, Task> onTurn = async view =>
            {
                if (beats++ > 0) await Task.Delay(TurnBeatDelayMs);
                await group.SendAsync("GameStateUpdated", GameViewMapper.ToDto(view));
            };

            var result = await _gameService.LeaveGameAsync(info.SessionId, info.UserId, onReplaced, onTurn);
            if (result.Success)
            {
                var leave = result.Value!;
                if (leave.ReplacedByAIUsername is not null)
                {
                    // Authoritative final state after the streamed beats (settles whose
                    // turn is next). Harmless duplicate of the last beat when one streamed.
                    if (leave.GameEnded)
                        await group.SendAsync("GameEnded", leave.StateAfterReplacement is not null
                            ? GameViewMapper.ToDto(leave.StateAfterReplacement) : (object)new { reason = "completed" });
                    else if (leave.StateAfterReplacement is not null)
                        await group.SendAsync("GameStateUpdated", GameViewMapper.ToDto(leave.StateAfterReplacement));
                }
                else if (leave.GameEnded)
                {
                    await group.SendAsync("GameEnded", new { reason = "disconnection" });
                }
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
