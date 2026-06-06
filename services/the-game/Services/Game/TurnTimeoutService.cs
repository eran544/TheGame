using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Hubs;

namespace TheGameServer.Services.Game;

public class TurnTimeoutService : BackgroundService
{
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

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
        var result = await gameService.TimeoutCurrentPlayerAsync(sessionId);

        if (!result.Success)
        {
            _logger.LogDebug("Timeout skipped for session {SessionId}: {Reason}", sessionId, result.Error);
            return;
        }

        var outcome = result.Value!;
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));

        _logger.LogInformation("Player {Player} timed out in session {SessionId}, replaced by {AI}",
            outcome.DisconnectedUsername, sessionId, outcome.ReplacedByAIUsername);

        await group.SendAsync("PlayerReplacedByAI", new
        {
            disconnectedUsername = outcome.DisconnectedUsername,
            aiUsername = outcome.ReplacedByAIUsername
        });

        if (outcome.GameEnded)
        {
            if (outcome.State is not null)
                await group.SendAsync("GameEnded", outcome.State);
            else
                await group.SendAsync("GameEnded", new { reason = outcome.EndReason });
        }
        else if (outcome.State is not null)
        {
            await group.SendAsync("GameStateUpdated", outcome.State);
        }
    }
}
