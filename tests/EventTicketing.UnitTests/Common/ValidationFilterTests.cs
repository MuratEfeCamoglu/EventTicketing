using EventTicketing.Api.Common;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EventTicketing.UnitTests.Common;

public sealed class ValidationFilterTests
{
    private sealed record SampleRequest(string Name);

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(r => r.Name).NotEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ValidRequest_CallsNext()
    {
        var filter = new ValidationFilter<SampleRequest>(new SampleValidator());
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new SampleRequest("ok"));

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));

        result.ShouldBe("next");
    }

    [Fact]
    public async Task InvokeAsync_InvalidRequest_Returns400ValidationFailedWithErrors()
    {
        var filter = new ValidationFilter<SampleRequest>(new SampleValidator());
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new SampleRequest(""));
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(null);
        });

        nextCalled.ShouldBeFalse();
        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["code"].ShouldBe("validation.failed");
        problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.ShouldContainKey("name");
    }
}
