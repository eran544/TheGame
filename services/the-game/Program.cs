using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using GameCommon.Auth;
using TheGameServer.Data;
using TheGameServer.Hubs;
using TheGameServer.Middleware;
using TheGameServer.Services;
using TheGameServer.Services.Chat;
using TheGameServer.Services.Game;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TheGame API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Shared platform auth: Redis connection, session store, and JWT validation
// (incl. the SignalR access_token handshake for /gamehub). Login/registration
// is owned by the Auth service — this service only validates tokens and keeps a
// local user projection (see UserProvisioningMiddleware).
builder.Services.AddPlatformAuth(builder.Configuration, "/gamehub");

builder.Services.AddScoped<IPasswordValidator, PasswordValidator>();
builder.Services.AddSingleton<IGameEngine, GameEngine>();
builder.Services.AddSingleton<IDeckShuffler, DeckShuffler>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddHttpClient("AiService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddHostedService<AdminInitializer>();
builder.Services.AddHostedService<TurnTimeoutService>();

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowClient");
app.UseAuthentication();
app.UseAuthorization();
// Provision a local user projection from JWT claims (auth lives in Auth service).
app.UseMiddleware<UserProvisioningMiddleware>();
app.MapControllers();
app.MapHub<GameHub>("/gamehub");

app.Run();
