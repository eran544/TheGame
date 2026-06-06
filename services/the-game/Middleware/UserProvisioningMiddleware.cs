using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;

namespace TheGameServer.Middleware;

/// <summary>
/// The Auth service is the sole identity authority. This service keeps a local
/// read-projection of users so existing game code (GamePlayer.User, stats,
/// admin) keeps working. On the first authenticated request from a user we have
/// not seen yet, provision a local User row from the validated JWT claims.
/// </summary>
public class UserProvisioningMiddleware
{
    private readonly RequestDelegate _next;

    public UserProvisioningMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var userId))
            {
                var exists = await db.Users.AnyAsync(u => u.Id == userId);
                if (!exists)
                {
                    var username = context.User.FindFirst("username")?.Value ?? "player";
                    var isAdmin = context.User.FindFirst("isAdmin")?.Value == "true";

                    db.Users.Add(new User
                    {
                        Id = userId,
                        Username = username,
                        PasswordHash = "*", // auth happens in the Auth service; never used here
                        IsAdmin = isAdmin,
                    });
                    db.PlayerStatistics.Add(new PlayerStatistics { UserId = userId });

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // A concurrent request provisioned the same user — safe to ignore.
                    }
                }
            }
        }

        await _next(context);
    }
}
