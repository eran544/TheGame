using System.ComponentModel.DataAnnotations;

namespace TheGameServer.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public Guid UserId { get; set; }

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public bool IsValidated { get; set; } = true;

    [MaxLength(255)]
    public string? ValidationReason { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public GameSession GameSession { get; set; } = null!;
    public User User { get; set; } = null!;
}
