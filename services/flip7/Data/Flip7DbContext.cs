using Microsoft.EntityFrameworkCore;
using Flip7Server.Models;

namespace Flip7Server.Data;

/// <summary>
/// Owns the Flip7DB. Greenfield — users are referenced by Guid and username is
/// denormalized from the JWT; the canonical identity lives in the Auth service,
/// so there is no local User table.
/// </summary>
public class Flip7DbContext : DbContext
{
    public Flip7DbContext(DbContextOptions<Flip7DbContext> options) : base(options) { }

    public DbSet<Flip7GameSession> GameSessions => Set<Flip7GameSession>();
    public DbSet<Flip7Player> Players => Set<Flip7Player>();
    public DbSet<Flip7RoundResult> RoundResults => Set<Flip7RoundResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flip7GameSession>(e =>
        {
            e.Property(g => g.Mode).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.RoundStateJson).HasColumnType("jsonb");
            e.HasIndex(g => g.Status);

            e.HasMany(g => g.Players)
                .WithOne(p => p.GameSession)
                .HasForeignKey(p => p.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(g => g.RoundResults)
                .WithOne(r => r.GameSession)
                .HasForeignKey(r => r.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Flip7Player>(e =>
        {
            e.Property(p => p.Username).HasMaxLength(64);
            e.Property(p => p.AiStyle).HasMaxLength(16);
            e.Property(p => p.AiDifficulty).HasMaxLength(16);
            e.HasIndex(p => new { p.GameSessionId, p.Seat }).IsUnique();
            e.HasIndex(p => p.UserId);
        });

        modelBuilder.Entity<Flip7RoundResult>(e =>
        {
            e.HasIndex(r => new { r.GameSessionId, r.RoundNumber });
        });
    }
}
