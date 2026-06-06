using FluentAssertions;
using GameCommon.Auth;

namespace TheGameServer.Tests.Services;

public class PasswordValidatorTests
{
    private readonly PasswordValidator _sut = new();

    [Theory]
    [InlineData("Test1234")]
    [InlineData("StrongP@ss1")]
    [InlineData("Abcdefg9")]
    public void Validate_WithValidPassword_ReturnsValid(string password)
    {
        var result = _sut.Validate(password);
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ab1")]
    [InlineData("Abcd123")]
    public void Validate_WhenTooShort_ReturnsInvalid(string password)
    {
        var result = _sut.Validate(password);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("at least 8 characters");
    }

    [Fact]
    public void Validate_WithNoUppercase_ReturnsInvalid()
    {
        var result = _sut.Validate("abcdefg1");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("uppercase");
    }

    [Fact]
    public void Validate_WithNoLowercase_ReturnsInvalid()
    {
        var result = _sut.Validate("ABCDEFG1");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("lowercase");
    }

    [Fact]
    public void Validate_WithNoDigit_ReturnsInvalid()
    {
        var result = _sut.Validate("Abcdefgh");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("digit");
    }
}
