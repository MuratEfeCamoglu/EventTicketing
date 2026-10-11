using EventTicketing.Api.Common;

namespace EventTicketing.UnitTests.Common;

public sealed class DevOnlyValuesTests
{
    [Theory]
    [InlineData("Dev-only-Passw0rd", true)]
    [InlineData("Host=localhost;Password=dev-only-postgres", true)]
    [InlineData("a-real-secret", false)]
    [InlineData(null, false)]
    public void Contains_Value_DetectsDevOnlyPrefixCaseInsensitively(string? value, bool expected)
    {
        DevOnlyValues.Contains(value).ShouldBe(expected);
    }
}
