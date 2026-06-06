using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using GameCommon.Auth;
using GameCommon.Identity;
using TheGameServer.Data;
using TheGameServer.DTOs.Auth;
using TheGameServer.Models;
using TheGameServer.Services;

namespace TheGameServer.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ISessionService> _sessions = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _jwt.Setup(j => j.GenerateToken(It.IsAny<IIdentityUser>(), out It.Ref<string>.IsAny))
            .Returns((IIdentityUser _, out string sessionId) =>
            {
                sessionId = "test-session-id";
                return "test-token";
            });

        _sut = new AuthService(_db, new PasswordValidator(), _jwt.Object, _sessions.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Register_WithValidRequest_CreatesUserAndStatistics()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Password = "Test1234",
            PasswordConfirmation = "Test1234"
        });

        result.Success.Should().BeTrue();
        result.Response!.Token.Should().Be("test-token");
        result.Response.User.Username.Should().Be("alice");

        (await _db.Users.SingleAsync()).Username.Should().Be("alice");
        (await _db.PlayerStatistics.SingleAsync()).TotalGames.Should().Be(0);
        _sessions.Verify(s => s.CreateAsync("test-session-id", It.IsAny<Guid>(), "alice", false), Times.Once);
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_ReturnsError()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Password = "Test1234",
            PasswordConfirmation = "Test5678"
        });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("do not match");
        (await _db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsError()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Password = "weak",
            PasswordConfirmation = "weak"
        });

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        (await _db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsError()
    {
        _db.Users.Add(new User { Username = "alice", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Password = "Test1234",
            PasswordConfirmation = "Test1234"
        });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already taken");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUpdatesLastLogin()
    {
        var user = new User
        {
            Username = "alice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234")
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "Test1234" });

        result.Success.Should().BeTrue();
        result.Response!.Token.Should().Be("test-token");
        var refreshed = await _db.Users.SingleAsync();
        refreshed.LastLoginAt.Should().NotBeNull();
        _sessions.Verify(s => s.CreateAsync(It.IsAny<string>(), user.Id, "alice", false), Times.Once);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsError()
    {
        _db.Users.Add(new User
        {
            Username = "alice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234")
        });
        await _db.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "Wrong1234" });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid");
        _sessions.Verify(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsError()
    {
        var result = await _sut.LoginAsync(new LoginRequest { Username = "nobody", Password = "Test1234" });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Logout_RevokesSession()
    {
        await _sut.LogoutAsync("session-xyz");

        _sessions.Verify(s => s.RevokeAsync("session-xyz"), Times.Once);
    }
}
