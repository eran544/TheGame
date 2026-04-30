using System.ComponentModel.DataAnnotations;

namespace TheGameServer.Models;

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatedBy { get; set; }

    [Required, MaxLength(20)]
    public string GamePhase { get; set; } = "lobby"; // lobby, playing, ended

    public int MaxPlayers { get; set; }
    public bool IsExpertMode { get; set; } = false;
    public string? CustomRules { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public User Creator { get; set; } = null!;
    public ICollection<GamePlayer> Players { get; set; } = new List<GamePlayer>();
    public GameState? State { get; set; }
    public GameResult? Result { get; set; }
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
