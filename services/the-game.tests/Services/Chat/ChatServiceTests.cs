using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using TheGameServer.Data;
using TheGameServer.Models;
using TheGameServer.Services.Chat;

namespace TheGameServer.Tests.Services.Chat;

public class ChatServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _sessionId;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    public ChatServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.AddRange(
            new User { Id = _aliceId, Username = "alice", PasswordHash = "x" },
            new User { Id = _bobId, Username = "bob", PasswordHash = "x" });

        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            CreatedBy = _aliceId,
            GamePhase = "playing",
            MaxPlayers = 2,
        };
        _sessionId = session.Id;
        session.Players.Add(new GamePlayer { UserId = _aliceId, GameSessionId = session.Id, PlayerIndex = 0 });
        session.Players.Add(new GamePlayer { UserId = _bobId, GameSessionId = session.Id, PlayerIndex = 1 });
        _db.GameSessions.Add(session);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────

    private ChatService BuildService(bool aiAllows = true, string aiReason = "", bool aiDown = false)
    {
        var handler = new Mock<HttpMessageHandler>();

        // Use Returns with a factory so each call gets a fresh HttpResponseMessage
        // (HttpContent streams can only be read once).
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, __) => Task.FromResult(
                aiDown
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(new { isAllowed = aiAllows, reason = aiReason }),
                            Encoding.UTF8,
                            "application/json")
                    }));

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:8000") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AiService")).Returns(httpClient);

        return new ChatService(_db, factory.Object);
    }

    // ── Allowed messages ─────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_WhenAiAllows_ReturnsFalseIsBlocked()
    {
        var sut = BuildService(aiAllows: true);

        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "I'm in trouble");

        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task SendMessage_WhenAiAllows_PersistsValidatedMessage()
    {
        var sut = BuildService(aiAllows: true);

        await sut.SendMessageAsync(_sessionId, _aliceId, "Things look good");

        var saved = await _db.ChatMessages.SingleAsync();
        saved.IsValidated.Should().BeTrue();
        saved.Message.Should().Be("Things look good");
    }

    [Fact]
    public async Task SendMessage_WhenAiAllows_ReturnsMessageRecord()
    {
        var sut = BuildService(aiAllows: true);

        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "Don't touch pile 1");

        result.Message.Should().NotBeNull();
        result.Message!.Username.Should().Be("alice");
        result.Message.UserId.Should().Be(_aliceId);
    }

    // ── Blocked messages ──────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_WhenAiBlocks_ReturnsIsBlockedTrue()
    {
        var sut = BuildService(aiAllows: false, aiReason: "Reveals card value");

        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "I have a 47");

        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Be("Reveals card value");
    }

    [Fact]
    public async Task SendMessage_WhenAiBlocks_PersistsInvalidatedMessage()
    {
        var sut = BuildService(aiAllows: false, aiReason: "Reveals card value");

        await sut.SendMessageAsync(_sessionId, _aliceId, "I have a 47");

        var saved = await _db.ChatMessages.SingleAsync();
        saved.IsValidated.Should().BeFalse();
        saved.ValidationReason.Should().Be("Reveals card value");
    }

    [Fact]
    public async Task SendMessage_WhenAiBlocks_ViolationCountIncremented()
    {
        var sut = BuildService(aiAllows: false);

        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "I have a 47");

        result.ViolationCount.Should().Be(1);
    }

    [Fact]
    public async Task SendMessage_MultipleViolations_CountAccumulates()
    {
        var sut = BuildService(aiAllows: false);

        await sut.SendMessageAsync(_sessionId, _aliceId, "I have a 47");
        await sut.SendMessageAsync(_sessionId, _aliceId, "Pile is at 34");
        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "Play below 40");

        result.ViolationCount.Should().Be(3);
    }

    // ── Violation threshold ───────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_AtViolationThreshold_BlocksWithoutCallingAi()
    {
        // Seed 5 existing violations
        for (var i = 0; i < 5; i++)
            _db.ChatMessages.Add(new ChatMessage
            {
                GameSessionId = _sessionId,
                UserId = _aliceId,
                Message = $"bad message {i}",
                IsValidated = false,
            });
        await _db.SaveChangesAsync();

        var handlerCalled = false;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => { handlerCalled = true; return new HttpResponseMessage(HttpStatusCode.OK); });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:8000") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AiService")).Returns(httpClient);
        var sut = new ChatService(_db, factory.Object);

        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "any message");

        result.IsBlocked.Should().BeTrue();
        handlerCalled.Should().BeFalse();
        result.BlockReason.Should().Contain("restricted");
    }

    // ── AI service down ───────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_WhenAiServiceDown_FailsOpen()
    {
        var sut = BuildService(aiDown: true);

        var result = await sut.SendMessageAsync(_sessionId, _aliceId, "I can help ascending");

        result.IsBlocked.Should().BeFalse(); // fail open
    }

    // ── History ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_ReturnsOnlyValidatedMessages()
    {
        _db.ChatMessages.AddRange(
            new ChatMessage { GameSessionId = _sessionId, UserId = _aliceId, Message = "good", IsValidated = true, User = _db.Users.First(u => u.Id == _aliceId) },
            new ChatMessage { GameSessionId = _sessionId, UserId = _bobId, Message = "bad", IsValidated = false, User = _db.Users.First(u => u.Id == _bobId) });
        await _db.SaveChangesAsync();

        var sut = BuildService();
        var history = await sut.GetHistoryAsync(_sessionId);

        history.Should().HaveCount(1);
        history[0].Message.Should().Be("good");
    }

    [Fact]
    public async Task GetHistory_ReturnsMessagesInChronologicalOrder()
    {
        var now = DateTime.UtcNow;
        _db.ChatMessages.AddRange(
            new ChatMessage { GameSessionId = _sessionId, UserId = _aliceId, Message = "first", IsValidated = true, SentAt = now, User = _db.Users.First(u => u.Id == _aliceId) },
            new ChatMessage { GameSessionId = _sessionId, UserId = _bobId, Message = "second", IsValidated = true, SentAt = now.AddSeconds(1), User = _db.Users.First(u => u.Id == _bobId) });
        await _db.SaveChangesAsync();

        var sut = BuildService();
        var history = await sut.GetHistoryAsync(_sessionId);

        history[0].Message.Should().Be("first");
        history[1].Message.Should().Be("second");
    }

    // ── Player not in session ─────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_PlayerNotInSession_ReturnsBlocked()
    {
        var outsider = Guid.NewGuid();
        _db.Users.Add(new User { Id = outsider, Username = "outsider", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        var sut = BuildService();
        var result = await sut.SendMessageAsync(_sessionId, outsider, "hello");

        result.IsBlocked.Should().BeTrue();
    }
}
