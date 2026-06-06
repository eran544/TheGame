namespace TheGameServer.Models;

public class PlayerStatistics
{
    public Guid UserId { get; set; }
    public int TotalGames { get; set; } = 0;
    public int PerfectGames { get; set; } = 0;
    public int? BestScore { get; set; } // Fewest remaining cards
    public decimal AverageRemainingCards { get; set; } = 0;
    public int TotalPlayTimeMinutes { get; set; } = 0;
    public int AIAssistedGames { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
