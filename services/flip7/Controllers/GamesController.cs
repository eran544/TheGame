using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Flip7Server.DTOs;
using Flip7Server.Models;
using Flip7Server.Services;

namespace Flip7Server.Controllers;

/// <summary>
/// REST surface for vs-AI and online Flip 7 games: create, join, start, and the
/// per-turn actions. Real-time clients use the Flip 7 hub for broadcasts; this
/// controller is the request/response equivalent (and how vs-AI games are
/// created before connecting). Action cards are auto-targeted by the engine's
/// default selector.
/// </summary>
[ApiController]
[Authorize]
[Route("api/flip7/games")]
public class GamesController : ControllerBase
{
    private readonly IFlip7GameService _games;

    public GamesController(IFlip7GameService games) => _games = games;

    [HttpPost("vs-ai")]
    public Task<ActionResult<Flip7GameStateDto>> CreateVsAi([FromBody] CreateGameRequest request, CancellationToken ct) =>
        Run(() =>
        {
            var (userId, username) = Identity();
            return _games.CreateGameAsync(Flip7GameMode.VsAi, userId, username, request?.AiPlayers ?? new List<Flip7AiSpec>(), request?.TargetScore, ct);
        });

    [HttpPost("online")]
    public Task<ActionResult<Flip7GameStateDto>> CreateOnline([FromBody] CreateGameRequest request, CancellationToken ct) =>
        Run(() =>
        {
            var (userId, username) = Identity();
            return _games.CreateGameAsync(Flip7GameMode.Online, userId, username, request?.AiPlayers ?? new List<Flip7AiSpec>(), request?.TargetScore, ct);
        });

    [HttpPost("{gameId:guid}/join")]
    public Task<ActionResult<Flip7GameStateDto>> Join(Guid gameId, CancellationToken ct) =>
        Run(() => { var (userId, username) = Identity(); return _games.JoinAsync(gameId, userId, username, ct); });

    [HttpPost("{gameId:guid}/start")]
    public Task<ActionResult<Flip7GameStateDto>> Start(Guid gameId, CancellationToken ct) =>
        Run(() => _games.StartAsync(gameId, Identity().userId, ct));

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<Flip7GameStateDto>> Get(Guid gameId, CancellationToken ct)
    {
        var state = await _games.GetStateAsync(gameId, Identity().userId, ct);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpPost("{gameId:guid}/hit")]
    public Task<ActionResult<Flip7GameStateDto>> Hit(Guid gameId, CancellationToken ct) =>
        Run(() => _games.HitAsync(gameId, Identity().userId, ct));

    [HttpPost("{gameId:guid}/stay")]
    public Task<ActionResult<Flip7GameStateDto>> Stay(Guid gameId, CancellationToken ct) =>
        Run(() => _games.StayAsync(gameId, Identity().userId, ct));

    [HttpPost("{gameId:guid}/next-round")]
    public Task<ActionResult<Flip7GameStateDto>> NextRound(Guid gameId, CancellationToken ct) =>
        Run(() => _games.NextRoundAsync(gameId, Identity().userId, ct));

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
