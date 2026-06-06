using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TheGameServer.Data;
using TheGameServer.Services.Game;

namespace TheGameServer.Tests.Services.Game;

/// <summary>
/// Creates a <see cref="GameService"/> wired for testing.
/// The AI HTTP client is stubbed so calls fail immediately and fall back to the greedy algorithm.
/// </summary>
internal static class GameServiceFactory
{
    public static GameService Create(AppDbContext db, IDeckShuffler shuffler)
    {
        var httpFactory = new Mock<System.Net.Http.IHttpClientFactory>();
        httpFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());   // no base address → PostAsync throws → greedy fallback

        return new GameService(
            db,
            new GameEngine(),
            shuffler,
            httpFactory.Object,
            NullLogger<GameService>.Instance);
    }
}
