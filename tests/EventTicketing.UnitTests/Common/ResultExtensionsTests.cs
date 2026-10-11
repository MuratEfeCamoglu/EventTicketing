using EventTicketing.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EventTicketing.UnitTests.Common;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData("validation.failed", 400)]
    [InlineData("auth.invalid_credentials", 401)]
    [InlineData("payment.declined", 402)]
    [InlineData("event.not_owner", 403)]
    [InlineData("resource.not_found", 404)]
    [InlineData("seat.unavailable", 409)]
    [InlineData("payment.idempotency_key_reused", 422)]
    [InlineData("rate_limit.exceeded", 429)]
    [InlineData("server.error", 500)]
    public void ToProblem_KnownCode_UsesStatusFromErrorTable(string code, int expectedStatus)
    {
        var result = new Error(code, "message").ToProblem().ShouldBeOfType<ProblemHttpResult>();

        result.StatusCode.ShouldBe(expectedStatus);
        result.ProblemDetails.Extensions["code"].ShouldBe(code);
        result.ProblemDetails.Detail.ShouldBe("message");
    }

    [Fact]
    public void ToProblem_ErrorWithExtensions_CopiesThemToProblemDetails()
    {
        var seatIds = new[] { Guid.CreateVersion7() };
        var error = new Error("seat.unavailable", "Seat is not available.", new Dictionary<string, object?> { ["unavailableSeatIds"] = seatIds });

        var result = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        result.ProblemDetails.Extensions["unavailableSeatIds"].ShouldBe(seatIds);
    }

    [Fact]
    public void ToProblem_UnknownCode_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new Error("not.a_code", "message").ToProblem());
    }

    [Fact]
    public void ToHttpResult_Success_InvokesSuccessMapping()
    {
        Result<int> result = 7;

        var httpResult = result.ToHttpResult(TypedResults.Ok);

        httpResult.ShouldBeOfType<Ok<int>>().Value.ShouldBe(7);
    }

    [Fact]
    public void ToHttpResult_Failure_ReturnsProblem()
    {
        Result<int> result = new Error("resource.not_found", "Not found.");

        var httpResult = result.ToHttpResult(TypedResults.Ok);

        httpResult.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}
