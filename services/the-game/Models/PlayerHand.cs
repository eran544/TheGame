namespace TheGameServer.Models;

public class PlayerHand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public Guid PlayerId { get; set; }
    public string Cards { get; set; } = "[]"; // JSON array
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public GameSession GameSession { get; set; } = null!;
    public GamePlayer Player { get; set; } = null!;
}
