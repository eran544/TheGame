namespace TheGameServer.Models;

public class GameState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public Guid? CurrentPlayerId { get; set; }
    public int AscendingPile1 { get; set; } = 1;
    public int AscendingPile2 { get; set; } = 1;
    public int DescendingPile1 { get; set; } = 100;
    public int DescendingPile2 { get; set; } = 100;
    public string DrawPileCards { get; set; } = "[]"; // JSON array
    public int PlayedCardsCount { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public GameSession GameSession { get; set; } = null!;
}
