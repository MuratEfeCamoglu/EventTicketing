using EventTicketing.Api.Features.Auth;

namespace EventTicketing.UnitTests.Features;

public sealed class RegisterValidatorTests
{
    private readonly Register.Validator _validator = new();

    [Theory]
    [InlineData("Passw0rd", true)]
    [InlineData("Şifre123", true)]
    [InlineData("Pass0rd", false)]
    [InlineData("onlyletters", false)]
    [InlineData("12345678", false)]
    [InlineData("", false)]
    public void Validate_Password_RequiresEightCharactersWithLetterAndDigit(string password, bool valid)
    {
        var result = _validator.Validate(new Register.Request("user@example.com", password, "User"));

        result.Errors.Any(e => e.PropertyName == nameof(Register.Request.Password)).ShouldBe(!valid);
    }

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    public void Validate_Email_RequiresEmailFormat(string email, bool valid)
    {
        var result = _validator.Validate(new Register.Request(email, "Passw0rd", "User"));

        result.Errors.Any(e => e.PropertyName == nameof(Register.Request.Email)).ShouldBe(!valid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void Validate_FullName_RequiresOneToHundredCharacters(int length, bool valid)
    {
        var result = _validator.Validate(new Register.Request("user@example.com", "Passw0rd", new string('a', length)));

        result.Errors.Any(e => e.PropertyName == nameof(Register.Request.FullName)).ShouldBe(!valid);
    }
}
