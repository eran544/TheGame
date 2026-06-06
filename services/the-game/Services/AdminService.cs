using Microsoft.EntityFrameworkCore;
using GameCommon.Auth;
using TheGameServer.Data;
using TheGameServer.DTOs.Admin;
using TheGameServer.Models;

namespace TheGameServer.Services;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardStatsAsync();
    Task<List<AdminUserDto>> GetAllUsersAsync();
    Task<(bool Success, string? Error)> CreateUserAsync(AdminCreateUserRequest request);
    Task<(bool Success, string? Error)> DeleteUserAsync(Guid userId);
    Task<(bool Success, string? Error)> ResetPasswordAsync(Guid userId, string newPassword);
    Task<List<AdminGameDto>> GetActiveGamesAsync();
    Task<(bool Success, string? Error)> ForceEndGameAsync(Guid sessionId);
    Task<(bool Success, string? Error)> KickPlayerAsync(Guid sessionId, Guid userId);
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IPasswordValidator _passwordValidator;

    public AdminService(AppDbContext db, IPasswordValidator passwordValidator)
    {
        _db = db;
        _passwordValidator = passwordValidator;
    }

    public async Task<AdminDashboardDto> GetDashboardStatsAsync()
    {
        var totalUsers = await _db.Users.CountAsync(u => !u.IsAI);
        var activeGames = await _db.GameSessions.CountAsync(s => s.GamePhase == "playing");
        var totalCompleted = await _db.GameSessions.CountAsync(s => s.GamePhase == "ended");
        var totalViolations = await _db.ChatMessages.CountAsync(m => !m.IsValidated);

        return new AdminDashboardDto(totalUsers, activeGames, totalCompleted, totalViolations);
    }

    public async Task<List<AdminUserDto>> GetAllUsersAsync()
    {
        var users = await _db.Users
            .Include(u => u.Statistics)
            .Where(u => !u.IsAI)
            .OrderBy(u => u.Username)
            .ToListAsync();

        return users.Select(u => new AdminUserDto(
            u.Id,
            u.Username,
            u.IsAdmin,
            u.CreatedAt,
            u.LastLoginAt,
            u.Statistics?.TotalGames ?? 0,
            u.Statistics?.PerfectGames ?? 0
        )).ToList();
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(AdminCreateUserRequest request)
    {
        var passwordCheck = _passwordValidator.Validate(request.Password);
        if (!passwordCheck.IsValid)
            return (false, passwordCheck.Error);

        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return (false, "Username is already taken");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsAdmin = request.IsAdmin
        };

        _db.Users.Add(user);
        _db.PlayerStatistics.Add(new PlayerStatistics { UserId = user.Id });
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return (false, "User not found");

        if (user.IsAdmin)
            return (false, "Cannot delete admin accounts");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return (false, "User not found");

        var passwordCheck = _passwordValidator.Validate(newPassword);
        if (!passwordCheck.IsValid)
            return (false, passwordCheck.Error);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<List<AdminGameDto>> GetActiveGamesAsync()
    {
        var sessions = await _db.GameSessions
            .Include(s => s.Creator)
            .Include(s => s.Players).ThenInclude(p => p.User)
            .Where(s => s.GamePhase == "playing")
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        return sessions.Select(s => new AdminGameDto(
            s.Id,
            s.Creator.Username,
            s.Players.Count(p => !p.IsSpectator),
            s.MaxPlayers,
            s.StartedAt ?? s.CreatedAt,
            s.Players
                .Where(p => !p.IsSpectator)
                .Select(p => new AdminGamePlayerDto(p.UserId, p.IsAI ? "AI" : p.User.Username, p.IsAI))
                .ToList()
        )).ToList();
    }

    public async Task<(bool Success, string? Error)> ForceEndGameAsync(Guid sessionId)
    {
        var session = await _db.GameSessions.FindAsync(sessionId);
        if (session is null)
            return (false, "Game session not found");

        if (session.GamePhase == "ended")
            return (false, "Game is already ended");

        session.GamePhase = "ended";
        session.EndedAt = DateTime.UtcNow;

        _db.GameResults.Add(new GameResult
        {
            GameSessionId = sessionId,
            TotalCardsRemaining = -1,
            EndReason = "admin_ended",
            CompletedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> KickPlayerAsync(Guid sessionId, Guid userId)
    {
        var player = await _db.GamePlayers
            .FirstOrDefaultAsync(p => p.GameSessionId == sessionId && p.UserId == userId && !p.IsAI);

        if (player is null)
            return (false, "Player not found in this game");

        var session = await _db.GameSessions.FindAsync(sessionId);
        if (session is null || session.GamePhase == "ended")
            return (false, "Game is not active");

        player.DisconnectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (true, null);
    }
}
