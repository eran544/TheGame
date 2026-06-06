using FluentAssertions;
using Moq;
using StackExchange.Redis;
using TheGameServer.Services;

namespace TheGameServer.Tests.Services;

public class SessionServiceTests
{
    private readonly Mock<IDatabase> _redis = new();
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redis.Object);
        _sut = new SessionService(multiplexer.Object);
    }

    [Fact]
    public async Task CreateAsync_StoresHashAndSetsTimeout()
    {
        var userId = Guid.NewGuid();

        await _sut.CreateAsync("sess-1", userId, "alice", isAdmin: false);

        _redis.Verify(r => r.HashSetAsync(
            "session:sess-1",
            It.Is<HashEntry[]>(entries =>
                entries.Any(e => e.Name == "userId" && e.Value == userId.ToString()) &&
                entries.Any(e => e.Name == "username" && e.Value == "alice") &&
                entries.Any(e => e.Name == "isAdmin" && e.Value == "False")),
            It.IsAny<CommandFlags>()), Times.Once);

        _redis.Verify(r => r.KeyExpireAsync(
            "session:sess-1",
            TimeSpan.FromMinutes(10),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task IsActiveAsync_ReturnsTrue_WhenKeyExists()
    {
        _redis.Setup(r => r.KeyExistsAsync("session:sess-1", It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);

        var result = await _sut.IsActiveAsync("sess-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveAsync_ReturnsFalse_WhenKeyMissing()
    {
        _redis.Setup(r => r.KeyExistsAsync("session:sess-1", It.IsAny<CommandFlags>()))
              .ReturnsAsync(false);

        var result = await _sut.IsActiveAsync("sess-1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TouchAsync_ExtendsTimeoutToTenMinutes()
    {
        await _sut.TouchAsync("sess-1");

        _redis.Verify(r => r.KeyExpireAsync(
            "session:sess-1",
            TimeSpan.FromMinutes(10),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_DeletesKey()
    {
        await _sut.RevokeAsync("sess-1");

        _redis.Verify(r => r.KeyDeleteAsync("session:sess-1", It.IsAny<CommandFlags>()), Times.Once);
    }
}
