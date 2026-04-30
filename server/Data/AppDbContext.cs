using Microsoft.EntityFrameworkCore;
using TheGameServer.Models;

namespace TheGameServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
    public DbSet<GameState> GameStates => Set<GameState>();
    public DbSet<PlayerHand> PlayerHands => Set<PlayerHand>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
    public DbSet<PlayerGameStat> PlayerGameStats => Set<PlayerGameStat>();
    public DbSet<PlayerStatistics> PlayerStatistics => Set<PlayerStatistics>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<UserSession>(e =>
        {
            e.HasIndex(s => s.SessionToken).IsUnique();
            e.HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId);
        });

        modelBuilder.Entity<GameSession>(e =>
        {
            e.HasOne(g => g.Creator).WithMany().HasForeignKey(g => g.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GamePlayer>(e =>
        {
            e.HasOne(p => p.GameSession).WithMany(g => g.Players).HasForeignKey(p => p.GameSessionId);
            e.HasOne(p => p.User).WithMany(u => u.GamePlayers).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GameState>(e =>
        {
            e.HasOne(s => s.GameSession).WithOne(g => g.State).HasForeignKey<GameState>(s => s.GameSessionId);
        });

        modelBuilder.Entity<PlayerHand>(e =>
        {
            e.HasOne(h => h.Player).WithOne(p => p.Hand).HasForeignKey<PlayerHand>(h => h.PlayerId);
            e.HasOne(h => h.GameSession).WithMany().HasForeignKey(h => h.GameSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GameResult>(e =>
        {
            e.HasOne(r => r.GameSession).WithOne(g => g.Result).HasForeignKey<GameResult>(r => r.GameSessionId);
        });

        modelBuilder.Entity<PlayerGameStat>(e =>
        {
            e.HasOne(s => s.GameResult).WithMany(r => r.PlayerStats).HasForeignKey(s => s.GameResultId);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerStatistics>(e =>
        {
            e.HasKey(s => s.UserId);
            e.HasOne(s => s.User).WithOne(u => u.Statistics).HasForeignKey<PlayerStatistics>(s => s.UserId);
            e.Property(s => s.AverageRemainingCards).HasPrecision(5, 2);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasOne(m => m.GameSession).WithMany(g => g.ChatMessages).HasForeignKey(m => m.GameSessionId);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
