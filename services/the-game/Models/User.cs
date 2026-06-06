using System.ComponentModel.DataAnnotations;
using GameCommon.Identity;

namespace TheGameServer.Models;

public class User : IIdentityUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; } = false;
    public bool IsAI { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<GamePlayer> GamePlayers { get; set; } = new List<GamePlayer>();
    public PlayerStatistics? Statistics { get; set; }
}
