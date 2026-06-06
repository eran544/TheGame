namespace GameCommon.Auth;

public interface IPasswordValidator
{
    PasswordValidationResult Validate(string password);
}

public record PasswordValidationResult(bool IsValid, string? Error);

public class PasswordValidator : IPasswordValidator
{
    public PasswordValidationResult Validate(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return new(false, "Password must be at least 8 characters");

        if (!password.Any(char.IsUpper))
            return new(false, "Password must contain at least one uppercase letter");

        if (!password.Any(char.IsLower))
            return new(false, "Password must contain at least one lowercase letter");

        if (!password.Any(char.IsDigit))
            return new(false, "Password must contain at least one digit");

        return new(true, null);
    }
}
