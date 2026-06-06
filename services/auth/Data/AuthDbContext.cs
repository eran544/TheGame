using Microsoft.EntityFrameworkCore;
using AuthServer.Models;

namespace AuthServer.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasOne(u => u.Profile)
                  .WithOne(p => p.User)
                  .HasForeignKey<PlayerProfile>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
