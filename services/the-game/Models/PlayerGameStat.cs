namespace TheGameServer.Models;

public class PlayerGameStat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameResultId { get; set; }
    public Guid UserId { get; set; }
    public int CardsInHand { get; set; }
    public bool WasReplacedByAI { get; set; } = false;
    public int? PlayTimeMinutes { get; set; }

    public GameResult GameResult { get; set; } = null!;
    public User User { get; set; } = null!;
}
