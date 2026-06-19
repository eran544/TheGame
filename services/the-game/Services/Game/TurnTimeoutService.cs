using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Hubs;
using TheGameServer.Mappers;

namespace TheGameServer.Services.Game;

public class TurnTimeoutService : BackgroundService
{
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    // Delay between streamed AI catch-up turns so a takeover reads like real turns.
    private const int TurnBeatDelayMs = 800;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hub;
    private readonly ILogger<TurnTimeoutService> _logger;

    public TurnTimeoutService(
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub> hub,
        ILogger<TurnTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            await CheckForTimedOutTurnsAsync(stoppingToken);
        }
    }

    private async Task CheckForTimedOutTurnsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - TurnTimeout;

        // Find active games where it's been a human player's turn for too long
        var timedOutSessions = await db.GameStates
            .Include(s => s.GameSession)
                .ThenInclude(gs => gs.Players)
                    .ThenInclude(p => p.User)
            .Where(s =>
                s.GameSession.GamePhase == "playing" &&
                s.CurrentTurnStartedAt != null &&
                s.CurrentTurnStartedAt < cutoff &&
                s.CurrentPlayerId != null)
            .Select(s => s.GameSessionId)
            .ToListAsync(ct);

        foreach (var sessionId in timedOutSessions)
        {
            try
            {
                await ProcessTimedOutSessionAsync(scope.ServiceProvider, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing turn timeout for session {SessionId}", sessionId);
            }
        }
    }

    private async Task ProcessTimedOutSessionAsync(IServiceProvider services, Guid sessionId)
    {
        var gameService = services.GetRequiredService<IGameService>();
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));

        // Announce the AI takeover, then stream the AI's catch-up turns paced like real
        // turns. This runs in the background service's own scope with an IHubContext, so
        // awaiting the paced beats here is safe.
        var beats = 0;
        Func<string, string, Task> onReplaced = (disconnected, ai) =>
        {
            _logger.LogInformation("Player {Player} timed out in session {SessionId}, replaced by {AI}",
                disconnected, sessionId, ai);
            return group.SendAsync("PlayerReplacedByAI", new { disconnectedUsername = disconnected, aiUsername = ai });
        };
        Func<GameStateView, Task> onTurn = async view =>
        {
            if (beats++ > 0) await Task.Delay(TurnBeatDelayMs);
            await group.SendAsync("GameStateUpdated", GameViewMapper.ToDto(view));
        };

        var result = await gameService.TimeoutCurrentPlayerAsync(sessionId, onReplaced, onTurn);

        if (!result.Success)
        {
            _logger.LogDebug("Timeout skipped for session {SessionId}: {Reason}", sessionId, result.Error);
            return;
        }

        var outcome = result.Value!;
        if (outcome.GameEnded)
        {
            if (outcome.State is not null)
                await group.SendAsync("GameEnded", GameViewMapper.ToDto(outcome.State));
            else
                await group.SendAsync("GameEnded", new { reason = outcome.EndReason });
        }
        else if (outcome.State is not null)
        {
            await group.SendAsync("GameStateUpdated", GameViewMapper.ToDto(outcome.State));
        }
    }
}
