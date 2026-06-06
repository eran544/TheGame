using System.ComponentModel.DataAnnotations;

namespace TheGameServer.Models;

public class GameResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public int TotalCardsRemaining { get; set; }
    public bool IsPerfectGame { get; set; } = false;
    public int? GameDurationMinutes { get; set; }

    [MaxLength(50)]
    public string? EndReason { get; set; } // completed, disconnection, admin_ended

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public GameSession GameSession { get; set; } = null!;
    public ICollection<PlayerGameStat> PlayerStats { get; set; } = new List<PlayerGameStat>();
}
