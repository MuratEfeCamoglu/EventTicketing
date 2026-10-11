using EventTicketing.Api.Common;

namespace EventTicketing.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_FromValue_ExposesValue()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_FromError_ExposesErrorAndThrowsOnValue()
    {
        Result<int> result = new Error("seat.unavailable", "Seat is not available.");

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("seat.unavailable");
        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
