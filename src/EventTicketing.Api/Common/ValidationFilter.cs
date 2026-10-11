using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EventTicketing.Api.Common;

/// <summary>Validates the endpoint argument of type <typeparamref name="T"/>; failure → 400 <c>validation.failed</c> with <c>errors</c>.</summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().SingleOrDefault();
        if (request is null)
        {
            return ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Request body is required."] });
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return result.IsValid ? await next(context) : ValidationProblem(result.ToDictionary());
    }

    private static ProblemHttpResult ValidationProblem(IDictionary<string, string[]> errors)
    {
        var problem = new HttpValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
        problem.Extensions["code"] = ErrorCodes.ValidationFailed;
        return TypedResults.Problem(problem);
    }
}
