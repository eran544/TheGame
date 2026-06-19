using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Flip7Server.DTOs;
using Flip7Server.Services;

namespace Flip7Server.Controllers;

/// <summary>
/// Single-player Flip 7 over plain request/response (no SignalR). The lone
/// active player self-targets all action cards, so no target selection is
/// needed. AI/online play lives on the Flip 7 hub.
/// </summary>
[ApiController]
[Authorize]
[Route("api/flip7/solo")]
public class SoloController : ControllerBase
{
    private readonly IFlip7GameService _games;

    public SoloController(IFlip7GameService games) => _games = games;

    [HttpPost]
    public async Task<ActionResult<Flip7GameStateDto>> Create([FromBody] CreateSoloRequest request, CancellationToken ct)
    {
        var (userId, username) = Identity();
        var state = await _games.CreateSoloAsync(userId, username, request?.TargetScore, ct);
        return CreatedAtAction(nameof(Get), new { gameId = state.Id }, state);
    }

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<Flip7GameStateDto>> Get(Guid gameId, CancellationToken ct)
    {
        var (userId, _) = Identity();
        var state = await _games.GetStateAsync(gameId, userId, ct);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpPost("{gameId:guid}/hit")]
    public Task<ActionResult<Flip7GameStateDto>> Hit(Guid gameId, CancellationToken ct) =>
        Run(() => _games.HitAsync(gameId, Identity().userId, ct: ct));

    [HttpPost("{gameId:guid}/stay")]
    public Task<ActionResult<Flip7GameStateDto>> Stay(Guid gameId, CancellationToken ct) =>
        Run(() => _games.StayAsync(gameId, Identity().userId, ct: ct));

    [HttpPost("{gameId:guid}/choose-target")]
    public Task<ActionResult<Flip7GameStateDto>> ChooseTarget(Guid gameId, [FromBody] ChooseTargetRequest request, CancellationToken ct) =>
        Run(() => _games.ChooseTargetAsync(gameId, Identity().userId, request.TargetPlayerId, ct: ct));

    [HttpPost("{gameId:guid}/next-round")]
    public Task<ActionResult<Flip7GameStateDto>> NextRound(Guid gameId, CancellationToken ct) =>
        Run(() => _games.NextRoundAsync(gameId, Identity().userId, ct: ct));

    private async Task<ActionResult<Flip7GameStateDto>> Run(Func<Task<Flip7GameStateDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private (Guid userId, string username) Identity()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst("username")?.Value ?? "player";
        return (Guid.TryParse(sub, out var id) ? id : Guid.Empty, username);
    }
}
