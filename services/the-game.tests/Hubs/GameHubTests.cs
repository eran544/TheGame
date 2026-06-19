using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TheGameServer.Hubs;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Hubs;

/// <summary>
/// Tests for GameHub connection tracking and disconnection handling (Task 11).
/// </summary>
public class GameHubTests
{
    private readonly Mock<IGameService> _mockService = new();
    private readonly Mock<IHubCallerClients> _mockClients = new();
    private readonly Mock<IClientProxy> _mockGroupProxy = new();
    private readonly Mock<IGroupManager> _mockGroups = new();
    private readonly Mock<HubCallerContext> _mockContext = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    // Unique per instance — prevents static _connections dict from leaking between tests.
    private readonly string _connId = Guid.NewGuid().ToString();

    private GameHub BuildHub()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _userId.ToString()) };
        _mockContext.Setup(c => c.ConnectionId).Returns(_connId);
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockGroupProxy.Object);
        _mockGroups
            .Setup(g => g.AddToGroupAsync(_connId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockGroups
            .Setup(g => g.RemoveFromGroupAsync(_connId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new GameHub(_mockService.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object,
        };
    }

    // ── JoinGame ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGame_AddsConnectionToGroupAndTracksSession()
    {
        var hub = BuildHub();

        await hub.JoinGame(_sessionId.ToString());

        _mockGroups.Verify(
            g => g.AddToGroupAsync(_connId, GameHub.GroupName(_sessionId.ToString()), default),
            Times.Once);

        // Verified indirectly: disconnect after join calls the service (see next test).
        // Clean up static tracking so the connection does not interfere with other tests.
        await hub.LeaveGame(_sessionId.ToString());
    }

    // ── LeaveGame ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveGame_RemovesConnectionFromGroup()
    {
        var hub = BuildHub();
        await hub.JoinGame(_sessionId.ToString());

        await hub.LeaveGame(_sessionId.ToString());

        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(_connId, GameHub.GroupName(_sessionId.ToString()), default),
            Times.Once);
    }

    // ── OnDisconnectedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_WhenConnectionNotTracked_DoesNotCallService()
    {
        var hub = BuildHub();
        // No JoinGame call — connection is not tracked.

        await hub.OnDisconnectedAsync(null);

        _mockService.Verify(
            s => s.LeaveGameAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()),
            Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ActiveGame_CallsLeaveServiceAndBroadcastsGameEnded()
    {
        _mockService
            .Setup(s => s.LeaveGameAsync(_sessionId, _userId,
                It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()))
            .ReturnsAsync(GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(true)));

        var hub = BuildHub();
        await hub.JoinGame(_sessionId.ToString());

        await hub.OnDisconnectedAsync(null);

        _mockService.Verify(s => s.LeaveGameAsync(_sessionId, _userId,
            It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()), Times.Once);
        _mockGroupProxy.Verify(
            p => p.SendCoreAsync(
                "GameEnded",
                It.Is<object?[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_LobbyOrAlreadyEndedGame_CallsLeaveServiceButNoBroadcast()
    {
        _mockService
            .Setup(s => s.LeaveGameAsync(_sessionId, _userId,
                It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()))
            .ReturnsAsync(GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(false)));

        var hub = BuildHub();
        await hub.JoinGame(_sessionId.ToString());

        await hub.OnDisconnectedAsync(null);

        _mockService.Verify(s => s.LeaveGameAsync(_sessionId, _userId,
            It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()), Times.Once);
        _mockGroupProxy.Verify(
            p => p.SendCoreAsync("GameEnded", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_AfterExplicitLeaveGame_DoesNotCallServiceAgain()
    {
        var hub = BuildHub();
        await hub.JoinGame(_sessionId.ToString());
        await hub.LeaveGame(_sessionId.ToString()); // explicit leave removes tracking

        await hub.OnDisconnectedAsync(null);

        _mockService.Verify(
            s => s.LeaveGameAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()),
            Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_GroupNameMatchesSession()
    {
        _mockService
            .Setup(s => s.LeaveGameAsync(_sessionId, _userId,
                It.IsAny<Func<string, string, Task>?>(), It.IsAny<Func<GameStateView, Task>?>()))
            .ReturnsAsync(GameResultEnvelope<LeaveResult>.Ok(new LeaveResult(true)));

        var hub = BuildHub();
        await hub.JoinGame(_sessionId.ToString());

        await hub.OnDisconnectedAsync(null);

        var expectedGroup = GameHub.GroupName(_sessionId.ToString());
        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
    }
}
