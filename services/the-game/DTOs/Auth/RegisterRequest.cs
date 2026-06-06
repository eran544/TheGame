using System.ComponentModel.DataAnnotations;

namespace TheGameServer.DTOs.Auth;

public class RegisterRequest
{
    [Required, MinLength(3), MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string PasswordConfirmation { get; set; } = string.Empty;
}
