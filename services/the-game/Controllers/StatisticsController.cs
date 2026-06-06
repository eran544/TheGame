using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheGameServer.Services;

namespace TheGameServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statsService;

    public StatisticsController(IStatisticsService statsService) => _statsService = statsService;

    [HttpGet("me")]
    public async Task<IActionResult> GetMyStatistics()
    {
        var stats = await _statsService.GetPlayerStatisticsAsync(GetUserId());
        return Ok(stats);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetGameHistory()
    {
        var history = await _statsService.GetGameHistoryAsync(GetUserId());
        return Ok(history);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
