using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace GameCommon.Auth;

/// <summary>
/// Shared platform authentication wiring. Every service (auth + each game)
/// validates the same JWT and checks the shared Redis session store, so a
/// single login is trusted everywhere.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// Registers the shared Redis connection, <see cref="ISessionService"/>, and
    /// JWT bearer validation (including the SignalR <c>access_token</c> query-string
    /// handshake for the given hub paths and the per-request session liveness check).
    /// </summary>
    public static IServiceCollection AddPlatformAuth(
        this IServiceCollection services,
        IConfiguration config,
        params string[] hubPaths)
    {
        var jwtSettings = config.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings missing");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")!));
        services.AddScoped<ISessionService, SessionService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // SignalR sends the token as a query string parameter for WebSocket/SSE
                        var token = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(token) &&
                            hubPaths.Any(h => path.StartsWithSegments(h)))
                        {
                            ctx.Token = token;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async ctx =>
                    {
                        var sessionId = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        if (string.IsNullOrEmpty(sessionId))
                        {
                            ctx.Fail("Missing session id");
                            return;
                        }

                        var sessions = ctx.HttpContext.RequestServices.GetRequiredService<ISessionService>();
                        if (!await sessions.IsActiveAsync(sessionId))
                        {
                            ctx.Fail("Session expired or revoked");
                            return;
                        }

                        await sessions.TouchAsync(sessionId);
                    }
                };
            });

        return services;
    }
}
