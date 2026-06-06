using System.ComponentModel.DataAnnotations;
using GameCommon.Identity;

namespace AuthServer.Models;

/// <summary>
/// Canonical platform identity. Lives in the shared AuthDB and is the single
/// source of truth for who a player is across every game. Game services
/// reference a user only by <see cref="Id"/>.
/// </summary>
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

    public PlayerProfile? Profile { get; set; }
}
