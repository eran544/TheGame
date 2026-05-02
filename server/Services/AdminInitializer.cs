using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;

namespace TheGameServer.Services;

public class AdminSettings
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
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
        if (string.IsNullOrEmpty(_settings.Password))
        {
            _logger.LogWarning("AdminSettings:Password is not configured - skipping super admin initialization");
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
