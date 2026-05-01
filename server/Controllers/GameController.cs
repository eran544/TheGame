using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheGameServer.DTOs.Game;
using TheGameServer.Services.Game;

namespace TheGameServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;

    public GameController(IGameService gameService) => _gameService = gameService;

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
        return Ok(new TurnOutcomeDto(MapState(outcome.State), outcome.GameEnded, outcome.EndReason));
    }

    [HttpPost("{sessionId:guid}/abandon")]
    public async Task<IActionResult> AbandonGame(Guid sessionId)
    {
        var result = await _gameService.AbandonGameAsync(sessionId, GetUserId());
        return result.Success ? Ok() : BadRequest(new { error = result.Error });
    }

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
            v.FinalScore.Rating.ToString()));
}
