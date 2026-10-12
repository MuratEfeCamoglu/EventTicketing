using EventTicketing.Api.Common;

namespace EventTicketing.UnitTests.Common;

public sealed class LikePatternTests
{
    [Theory]
    [InlineData(" Istanbul ", "Istanbul")]
    [InlineData("100%", @"100\%")]
    [InlineData("a_b", @"a\_b")]
    [InlineData(@"c:\x", @"c:\\x")]
    public void Exact_EscapesWildcardsAndTrims(string text, string expected) => LikePattern.Exact(text).ShouldBe(expected);

    [Fact]
    public void Contains_WrapsEscapedTextInWildcards() => LikePattern.Contains(" 50% ").ShouldBe(@"%50\%%");
}
