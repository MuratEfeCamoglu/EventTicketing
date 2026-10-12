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

    [Fact]
    public void NonGeneric_SuccessAndFailure_ExposeError()
    {
        Result failure = new Error("resource.not_found", "Not found.");

        Result.Success().IsSuccess.ShouldBeTrue();
        failure.IsSuccess.ShouldBeFalse();
        failure.Error.Code.ShouldBe("resource.not_found");
    }
}
