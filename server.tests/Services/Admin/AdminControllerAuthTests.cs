using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TheGameServer.Controllers;
using TheGameServer.DTOs.Admin;
using TheGameServer.Services;

namespace TheGameServer.Tests.Services.Admin;

/// <summary>
/// Verifies that every AdminController action returns 403 Forbidden
/// when called by a non-admin user, and succeeds (non-403) when called
/// by an admin user.
/// </summary>
public class AdminControllerAuthTests
{
    private static AdminController BuildController(bool isAdmin)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("isAdmin", isAdmin ? "true" : "false"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var service = new Mock<IAdminService>();

        // Set up service stubs so the controller can actually run through after the auth check
        service.Setup(s => s.GetDashboardStatsAsync())
            .ReturnsAsync(new AdminDashboardDto(0, 0, 0, 0));
        service.Setup(s => s.GetAllUsersAsync())
            .ReturnsAsync(new List<AdminUserDto>());
        service.Setup(s => s.CreateUserAsync(It.IsAny<AdminCreateUserRequest>()))
            .ReturnsAsync((true, (string?)null));
        service.Setup(s => s.DeleteUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync((true, (string?)null));
        service.Setup(s => s.ResetPasswordAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((true, (string?)null));
        service.Setup(s => s.GetActiveGamesAsync())
            .ReturnsAsync(new List<AdminGameDto>());
        service.Setup(s => s.ForceEndGameAsync(It.IsAny<Guid>()))
            .ReturnsAsync((true, (string?)null));
        service.Setup(s => s.KickPlayerAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((true, (string?)null));

        var controller = new AdminController(service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    // ── Non-admin is rejected ─────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.GetDashboard();
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetUsers_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.GetUsers();
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateUser_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.CreateUser(new AdminCreateUserRequest("x", "y"));
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteUser_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.DeleteUser(Guid.NewGuid());
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ResetPassword_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.ResetPassword(Guid.NewGuid(), new AdminResetPasswordRequest("new"));
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetActiveGames_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.GetActiveGames();
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ForceEndGame_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.ForceEndGame(Guid.NewGuid());
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task KickPlayer_NonAdmin_Returns403()
    {
        var ctrl = BuildController(isAdmin: false);
        var result = await ctrl.KickPlayer(Guid.NewGuid(), Guid.NewGuid());
        result.Should().BeOfType<ForbidResult>();
    }

    // ── Admin is allowed through ──────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_Admin_ReturnsOk()
    {
        var ctrl = BuildController(isAdmin: true);
        var result = await ctrl.GetDashboard();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUsers_Admin_ReturnsOk()
    {
        var ctrl = BuildController(isAdmin: true);
        var result = await ctrl.GetUsers();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateUser_Admin_ReturnsOk()
    {
        var ctrl = BuildController(isAdmin: true);
        var result = await ctrl.CreateUser(new AdminCreateUserRequest("x", "y"));
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetActiveGames_Admin_ReturnsOk()
    {
        var ctrl = BuildController(isAdmin: true);
        var result = await ctrl.GetActiveGames();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ForceEndGame_Admin_ReturnsOk()
    {
        var ctrl = BuildController(isAdmin: true);
        var result = await ctrl.ForceEndGame(Guid.NewGuid());
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task KickPlayer_Admin_ReturnsOk()
    {
        var ctrl = BuildController(isAdmin: true);
        var result = await ctrl.KickPlayer(Guid.NewGuid(), Guid.NewGuid());
        result.Should().BeOfType<OkObjectResult>();
    }
}
