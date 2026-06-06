using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheGameServer.DTOs.Admin;
using TheGameServer.Services;

namespace TheGameServer.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    // ── Dashboard ──────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        if (!IsAdmin()) return Forbid();
        return Ok(await _admin.GetDashboardStatsAsync());
    }

    // ── Users ──────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        if (!IsAdmin()) return Forbid();
        return Ok(await _admin.GetAllUsersAsync());
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest request)
    {
        if (!IsAdmin()) return Forbid();
        var (success, error) = await _admin.CreateUserAsync(request);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "User created" });
    }

    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        if (!IsAdmin()) return Forbid();
        var (success, error) = await _admin.DeleteUserAsync(userId);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "User deleted" });
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] AdminResetPasswordRequest request)
    {
        if (!IsAdmin()) return Forbid();
        var (success, error) = await _admin.ResetPasswordAsync(userId, request.NewPassword);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Password reset" });
    }

    // ── Games ──────────────────────────────────────────────────────────────

    [HttpGet("games")]
    public async Task<IActionResult> GetActiveGames()
    {
        if (!IsAdmin()) return Forbid();
        return Ok(await _admin.GetActiveGamesAsync());
    }

    [HttpPost("games/{sessionId:guid}/force-end")]
    public async Task<IActionResult> ForceEndGame(Guid sessionId)
    {
        if (!IsAdmin()) return Forbid();
        var (success, error) = await _admin.ForceEndGameAsync(sessionId);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Game ended" });
    }

    [HttpPost("games/{sessionId:guid}/kick/{userId:guid}")]
    public async Task<IActionResult> KickPlayer(Guid sessionId, Guid userId)
    {
        if (!IsAdmin()) return Forbid();
        var (success, error) = await _admin.KickPlayerAsync(sessionId, userId);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Player kicked" });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private bool IsAdmin()
    {
        var claim = User.FindFirst("isAdmin")?.Value;
        return claim == "true";
    }
}
