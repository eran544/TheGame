using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TheGameServer.DTOs.Game;
using TheGameServer.Hubs;
using TheGameServer.Mappers;
using TheGameServer.Services.Game;

namespace TheGameServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly IHubContext<GameHub> _hub;

    public GameController(IGameService gameService, IHubContext<GameHub> hub)
    {
        _gameService = gameService;
        _hub = hub;
    }

    // ── Single-player ───────────────────────────────────────────────────────

    [HttpPost("start")]
    public async Task<IActionResult> StartGame([FromBody] StartGameRequest request)
    {
        var result = await _gameService.StartSinglePlayerGameAsync(GetUserId(), request.IsExpertMode);
        return result.Success ? Ok(MapState(result.Value!)) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetGameState(Guid sessionId)
    {
        var result = await _gameService.GetGameStateAsync(sessionId, GetUserId());
        return result.Success ? Ok(MapState(result.Value!)) : BadRequest(new { error = result.Error });
    }

    // Delay between streamed turns so AI players appear to take real turns.
    private const int TurnBeatDelayMs = 800;

    [HttpPost("{sessionId:guid}/turn")]
    public async Task<IActionResult> PlayTurn(Guid sessionId, [FromBody] PlayTurnRequest request)
    {
        var plays = request.Plays.Select(p => new CardPlay(p.Card, (PileSlot)p.Slot)).ToList();
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));

        // Stream each turn as it resolves — the human's move first (immediately),
        // then each AI's, paced — so the whole group watches play unfold in order.
        var beats = 0;
        Func<GameStateView, Task> onTurn = async view =>
        {
            if (beats++ > 0) await Task.Delay(TurnBeatDelayMs);
            await group.SendAsync("GameStateUpdated", MapState(view));
        };

        var result = await _gameService.PlayTurnAsync(sessionId, GetUserId(), plays, onTurn);
        if (!result.Success) return BadRequest(new { error = result.Error });
        var outcome = result.Value!;
        var dto = new TurnOutcomeDto(MapState(outcome.State), outcome.GameEnded, outcome.EndReason);
        // Authoritative final state for the group (settles whose turn is next after
        // any streamed beats). Harmless when nothing was streamed (single-player).
        await group.SendAsync("GameStateUpdated", dto.State);
        if (outcome.GameEnded)
            await group.SendAsync("GameEnded", dto.State);
        return Ok(dto);
    }

    [HttpPost("{sessionId:guid}/abandon")]
    public async Task<IActionResult> AbandonGame(Guid sessionId)
    {
        var result = await _gameService.AbandonGameAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("GameEnded", new { reason = "abandoned" });
        return Ok();
    }

    [HttpPost("{sessionId:guid}/undo")]
    public async Task<IActionResult> UndoLastMove(Guid sessionId)
    {
        var result = await _gameService.UndoLastMoveAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });
        var dto = MapState(result.Value!);
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("GameStateUpdated", dto);
        return Ok(dto);
    }

    // ── Multiplayer lobby ───────────────────────────────────────────────────

    [HttpPost("multiplayer/create")]
    public async Task<IActionResult> CreateMultiplayerGame([FromBody] CreateMultiplayerGameRequest request)
    {
        var result = await _gameService.CreateMultiplayerGameAsync(GetUserId(), request.MaxPlayers, request.IsExpertMode);
        return result.Success ? Ok(MapLobby(result.Value!)) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{sessionId:guid}/join")]
    public async Task<IActionResult> JoinGame(Guid sessionId)
    {
        var result = await _gameService.JoinGameAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });
        var dto = MapLobby(result.Value!);
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("LobbyUpdated", dto);
        return Ok(dto);
    }

    [HttpPost("{sessionId:guid}/leave")]
    public async Task<IActionResult> LeaveGame(Guid sessionId)
    {
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));

        // Announce any AI takeover, then stream the AI's catch-up turns paced like real
        // turns — same as a dropped connection (see GameHub.OnDisconnectedAsync).
        var beats = 0;
        Func<string, string, Task> onReplaced = (disconnected, ai) =>
            group.SendAsync("PlayerReplacedByAI", new { disconnectedUsername = disconnected, aiUsername = ai });
        Func<GameStateView, Task> onTurn = async view =>
        {
            if (beats++ > 0) await Task.Delay(TurnBeatDelayMs);
            await group.SendAsync("GameStateUpdated", MapState(view));
        };

        var result = await _gameService.LeaveGameAsync(sessionId, GetUserId(), onReplaced, onTurn);
        if (!result.Success) return BadRequest(new { error = result.Error });

        var leave = result.Value!;
        if (leave.ReplacedByAIUsername is not null)
        {
            if (leave.GameEnded)
                await group.SendAsync("GameEnded", leave.StateAfterReplacement is not null
                    ? MapState(leave.StateAfterReplacement) : (object)new { reason = "completed" });
            else if (leave.StateAfterReplacement is not null)
                await group.SendAsync("GameStateUpdated", MapState(leave.StateAfterReplacement));
        }
        else
        {
            await group.SendAsync("PlayerLeft", GetUserId());
            if (leave.GameEnded)
                await group.SendAsync("GameEnded", new { reason = "player_left" });
        }

        return Ok();
    }

    [HttpPost("{sessionId:guid}/reconnect")]
    public async Task<IActionResult> ReconnectToGame(Guid sessionId)
    {
        var result = await _gameService.ReconnectPlayerAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });

        var dto = MapState(result.Value!.State);
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));
        await group.SendAsync("PlayerReconnected", new { username = result.Value.ReconnectedUsername });
        await group.SendAsync("GameStateUpdated", dto);
        return Ok(dto);
    }

    [HttpGet("{sessionId:guid}/lobby")]
    public async Task<IActionResult> GetLobbyState(Guid sessionId)
    {
        var result = await _gameService.GetLobbyStateAsync(sessionId, GetUserId());
        return result.Success ? Ok(MapLobby(result.Value!)) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{sessionId:guid}/multiplayer/start")]
    public async Task<IActionResult> StartMultiplayerGame(Guid sessionId)
    {
        var result = await _gameService.StartMultiplayerGameAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });
        var dto = MapState(result.Value!);
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("GameStarted", dto);
        return Ok(dto);
    }

    [HttpPost("{sessionId:guid}/add-ai")]
    public async Task<IActionResult> AddAIPlayer(Guid sessionId)
    {
        var result = await _gameService.AddAIPlayerAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });
        var dto = MapLobby(result.Value!);
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("LobbyUpdated", dto);
        return Ok(dto);
    }

    [HttpDelete("{sessionId:guid}/remove-ai/{aiUserId:guid}")]
    public async Task<IActionResult> RemoveAIPlayer(Guid sessionId, Guid aiUserId)
    {
        var result = await _gameService.RemoveAIPlayerAsync(sessionId, GetUserId(), aiUserId);
        if (!result.Success) return BadRequest(new { error = result.Error });
        var dto = MapLobby(result.Value!);
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("LobbyUpdated", dto);
        return Ok(dto);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static GameStateDto MapState(GameStateView v) => GameViewMapper.ToDto(v);

    private static LobbyStateDto MapLobby(LobbyView v) => new(
        v.SessionId,
        v.GamePhase,
        v.Players.Select(p => new LobbyPlayerDto(p.UserId, p.Username, p.PlayerIndex, p.IsAI)).ToList(),
        v.MaxPlayers,
        v.IsExpertMode,
        v.CanStart,
        v.CreatedBy);
}
