using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;

namespace TheGameServer.Services;

public class AdminSettings
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
}

public static class AiPlayerConstants
{
    public static readonly (Guid Id, string Username)[] AiUsers =
    [
        (new Guid("00000000-0000-0000-0001-000000000001"), "AI Player 1"),
        (new Guid("00000000-0000-0000-0001-000000000002"), "AI Player 2"),
        (new Guid("00000000-0000-0000-0001-000000000003"), "AI Player 3"),
        (new Guid("00000000-0000-0000-0001-000000000004"), "AI Player 4"),
    ];

    public static readonly Guid[] Ids = AiUsers.Select(u => u.Id).ToArray();

    // Each seeded AI account plays with a distinct style/difficulty so opponents
    // feel different. Choosing which AI to add to the lobby therefore chooses how
    // it plays. (Per-player style selection will move to a persisted setting later.)
    private static readonly IReadOnlyDictionary<Guid, (string Style, string Difficulty)> Profiles =
        new Dictionary<Guid, (string, string)>
        {
            [AiUsers[0].Id] = ("safe", "medium"),
            [AiUsers[1].Id] = ("balanced", "medium"),
            [AiUsers[2].Id] = ("risky", "medium"),
            [AiUsers[3].Id] = ("risky", "hard"),
        };

    public static (string Style, string Difficulty) GetProfile(Guid id) =>
        Profiles.TryGetValue(id, out var p) ? p : ("balanced", "medium");
}

public class AdminInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AdminInitializer> _logger;
    private readonly AdminSettings _settings;

    public AdminInitializer(IServiceProvider services, IConfiguration config, ILogger<AdminInitializer> logger)
    {
        _services = services;
        _logger = logger;
        _settings = config.GetSection("AdminSettings").Get<AdminSettings>() ?? new AdminSettings();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
        await SeedAdminAsync(db, cancellationToken);
        await SeedAIPlayersAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAdminAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.Password))
        {
            _logger.LogWarning("AdminSettings:Password is not configured - skipping super admin initialization");
            return;
        }

        var exists = await db.Users.AnyAsync(u => u.Username == _settings.Username, cancellationToken);
        if (exists)
        {
            _logger.LogInformation("Super admin '{Username}' already exists", _settings.Username);
            return;
        }

        var admin = new User
        {
            Username = _settings.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(_settings.Password),
            IsAdmin = true
        };

        db.Users.Add(admin);
        db.PlayerStatistics.Add(new PlayerStatistics { UserId = admin.Id });
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Super admin '{Username}' created", _settings.Username);
    }

    private async Task SeedAIPlayersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        bool any = false;
        foreach (var (id, username) in AiPlayerConstants.AiUsers)
        {
            if (!await db.Users.AnyAsync(u => u.Id == id, cancellationToken))
            {
                db.Users.Add(new User
                {
                    Id = id,
                    Username = username,
                    PasswordHash = "*",
                    IsAI = true,
                });
                _logger.LogInformation("AI user '{Username}' created", username);
                any = true;
            }
        }

        if (any)
            await db.SaveChangesAsync(cancellationToken);
    }
}
