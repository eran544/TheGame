using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TheGameServer.Hubs;

[Authorize]
public class GameHub : Hub
{
    // Client calls these to subscribe/unsubscribe from a game session group.
    public async Task JoinGame(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
    }

    public async Task LeaveGame(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));
    }

    public static string GroupName(string sessionId) => $"game:{sessionId}";

    // Server broadcasts to group (sent from GameController via IHubContext):
    //   "LobbyUpdated"    → LobbyStateDto   (player joined/lobby changed)
    //   "PlayerLeft"      → Guid userId      (player left lobby or game)
    //   "GameStarted"     → GameStateDto     (lobby → playing)
    //   "GameStateUpdated"→ GameStateDto     (after each turn / undo)
    //   "GameEnded"       → GameStateDto     (game over)
}
