using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flip7Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Flip7Controller : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", service = "flip7" });

    /// <summary>
    /// Authenticated ping. Confirms this service accepts a platform JWT issued
    /// by the Auth service. The real Flip 7 game endpoints arrive in Phase 2.
    /// </summary>
    [Authorize]
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst("username")?.Value;
        return Ok(new { ok = true, userId, username, message = "Flip 7 service is ready (Phase 2)." });
    }
}
