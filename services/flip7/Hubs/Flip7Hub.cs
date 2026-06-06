using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Flip7Server.Hubs;

/// <summary>
/// Real-time hub for Flip 7 lobbies and games. Phase 1 stub: just the
/// group join/leave plumbing (one SignalR group per game session), mirroring
/// The Game's hub. Game events are broadcast here in Phase 2.
/// </summary>
[Authorize]
public class Flip7Hub : Hub
{
    public Task JoinGame(string sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public Task LeaveGame(string sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public static string GroupName(string sessionId) => $"flip7:{sessionId}";
}
