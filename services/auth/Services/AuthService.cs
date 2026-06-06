using Microsoft.EntityFrameworkCore;
using GameCommon.Auth;
using AuthServer.Data;
using AuthServer.DTOs.Auth;
using AuthServer.Models;

namespace AuthServer.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task LogoutAsync(string sessionId);
}

public record AuthResult(bool Success, AuthResponse? Response, string? Error);

public class AuthService : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IJwtService _jwt;
    private readonly ISessionService _sessions;

    public AuthService(AuthDbContext db, IPasswordValidator passwordValidator, IJwtService jwt, ISessionService sessions)
    {
        _db = db;
        _passwordValidator = passwordValidator;
        _jwt = jwt;
        _sessions = sessions;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        if (request.Password != request.PasswordConfirmation)
            return new(false, null, "Password and confirmation do not match");

        var passwordCheck = _passwordValidator.Validate(request.Password);
        if (!passwordCheck.IsValid)
            return new(false, null, passwordCheck.Error);

        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return new(false, null, "Username is already taken");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        _db.PlayerProfiles.Add(new PlayerProfile { UserId = user.Id });
        await _db.SaveChangesAsync();

        return new(true, await IssueAuthAsync(user), null);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new(false, null, "Invalid username or password");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new(true, await IssueAuthAsync(user), null);
    }

    public Task LogoutAsync(string sessionId) => _sessions.RevokeAsync(sessionId);

    private async Task<AuthResponse> IssueAuthAsync(User user)
    {
        var token = _jwt.GenerateToken(user, out var sessionId);
        await _sessions.CreateAsync(sessionId, user.Id, user.Username, user.IsAdmin);

        return new AuthResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            }
        };
    }
}
