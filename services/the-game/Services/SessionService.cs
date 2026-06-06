using StackExchange.Redis;

namespace TheGameServer.Services;

public interface ISessionService
{
    Task CreateAsync(string sessionId, Guid userId, string username, bool isAdmin);
    Task<bool> IsActiveAsync(string sessionId);
    Task TouchAsync(string sessionId);
    Task RevokeAsync(string sessionId);
}

public class SessionService : ISessionService
{
    private readonly IDatabase _db;
    private static readonly TimeSpan LobbyInactivityTimeout = TimeSpan.FromMinutes(10);

    public SessionService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string Key(string sessionId) => $"session:{sessionId}";

    public async Task CreateAsync(string sessionId, Guid userId, string username, bool isAdmin)
    {
        var key = Key(sessionId);
        var entries = new HashEntry[]
        {
            new("userId", userId.ToString()),
            new("username", username),
            new("isAdmin", isAdmin.ToString())
        };

        await _db.HashSetAsync(key, entries);
        await _db.KeyExpireAsync(key, LobbyInactivityTimeout);
    }

    public async Task<bool> IsActiveAsync(string sessionId)
    {
        return await _db.KeyExistsAsync(Key(sessionId));
    }

    public async Task TouchAsync(string sessionId)
    {
        await _db.KeyExpireAsync(Key(sessionId), LobbyInactivityTimeout);
    }

    public async Task RevokeAsync(string sessionId)
    {
        await _db.KeyDeleteAsync(Key(sessionId));
    }
}
