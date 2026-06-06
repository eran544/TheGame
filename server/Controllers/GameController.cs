using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TheGameServer.DTOs.Game;
using TheGameServer.Hubs;
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

    [HttpPost("{sessionId:guid}/turn")]
    public async Task<IActionResult> PlayTurn(Guid sessionId, [FromBody] PlayTurnRequest request)
    {
        var plays = request.Plays.Select(p => new CardPlay(p.Card, (PileSlot)p.Slot)).ToList();
        var result = await _gameService.PlayTurnAsync(sessionId, GetUserId(), plays);
        if (!result.Success) return BadRequest(new { error = result.Error });
        var outcome = result.Value!;
        var dto = new TurnOutcomeDto(MapState(outcome.State), outcome.GameEnded, outcome.EndReason);
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));
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
        var result = await _gameService.LeaveGameAsync(sessionId, GetUserId());
        if (!result.Success) return BadRequest(new { error = result.Error });

        var leave = result.Value!;
        var group = _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()));

        if (leave.ReplacedByAIUsername is not null)
        {
            await group.SendAsync("PlayerReplacedByAI", new
            {
                disconnectedUsername = leave.DisconnectedUsername,
                aiUsername = leave.ReplacedByAIUsername
            });
            if (leave.GameEnded)
                await group.SendAsync("GameEnded", leave.StateAfterReplacement ?? (object)new { reason = "completed" });
            else if (leave.StateAfterReplacement is not null)
                await group.SendAsync("GameStateUpdated", leave.StateAfterReplacement);
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

    private static GameStateDto MapState(GameStateView v) => new(
        v.SessionId,
        v.GamePhase,
        v.IsExpertMode,
        new PileTopsDto(v.Piles.Ascending1, v.Piles.Ascending2, v.Piles.Descending1, v.Piles.Descending2),
        v.DrawPileCount,
        v.PlayedCardsCount,
        v.Hand,
        v.MinCardsThisTurn,
        v.FinalScore is null ? null : new FinalScoreDto(
            v.FinalScore.CardsRemaining,
            v.FinalScore.IsPerfectGame,
            v.FinalScore.Rating.ToString()),
        v.CanUndo,
        v.CurrentPlayerId,
        v.Players?.Select(p => new PlayerInGameDto(p.UserId, p.Username, p.HandCount, p.IsAI, p.IsCurrentTurn, p.IsDisconnected)).ToList(),
        v.RecentMoves?.Select(m => new LastMoveDto(
            m.PlayerUsername,
            m.Plays.Select(p => new LastMovePlayDto(p.Card, p.PileSlot)).ToList())).ToList());

    private static LobbyStateDto MapLobby(LobbyView v) => new(
        v.SessionId,
        v.GamePhase,
        v.Players.Select(p => new LobbyPlayerDto(p.UserId, p.Username, p.PlayerIndex, p.IsAI)).ToList(),
        v.MaxPlayers,
        v.IsExpertMode,
        v.CanStart,
        v.CreatedBy);
}
