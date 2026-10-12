using EventTicketing.Api.Infrastructure.Observability;

namespace EventTicketing.UnitTests.Infrastructure;

public sealed class LogMaskingTests
{
    [Theory]
    [InlineData("ayse@example.com", "a***@example.com")]
    [InlineData("a@example.com", "a***@example.com")]
    [InlineData("@example.com", "***")]
    [InlineData("no-at-sign", "***")]
    public void MaskEmail_Value_KeepsFirstCharacterAndDomain(string email, string expected)
    {
        LogMasking.MaskEmail(email).ShouldBe(expected);
    }
}
