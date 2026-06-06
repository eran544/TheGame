namespace TheGameServer.Models;

public class GamePlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public Guid UserId { get; set; }
    public int PlayerIndex { get; set; }
    public bool IsAI { get; set; } = false;
    public bool IsSpectator { get; set; } = false;
    public bool ReplacedByAI { get; set; } = false;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public User User { get; set; } = null!;
    public PlayerHand? Hand { get; set; }
}
