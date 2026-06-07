using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using GameCommon.Auth;
using Flip7Server.Data;
using Flip7Server.Game;
using Flip7Server.Hubs;
using Flip7Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Flip 7 API", Version = "v1" });

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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
});
builder.Services.AddSignalR();

builder.Services.AddDbContext<Flip7DbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Shared platform auth: same JwtSettings as the auth + the-game services, plus
// the SignalR access_token handshake for the Flip 7 hub.
builder.Services.AddPlatformAuth(builder.Configuration, "/flip7hub");
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IFlip7DeckShuffler, Flip7DeckShuffler>();
builder.Services.AddScoped<IFlip7GameService, Flip7GameService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Apply migrations on startup so Flip7DB is created/updated automatically in dev.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Flip7DbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<Flip7Hub>("/flip7hub");

app.Run();
