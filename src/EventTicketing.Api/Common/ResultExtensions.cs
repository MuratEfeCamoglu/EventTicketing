using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        if (!ErrorCodes.StatusByCode.TryGetValue(error.Code, out var status))
        {
            throw new InvalidOperationException($"Error code '{error.Code}' is not in the ISKELET §5.12 table.");
        }

        var problem = new ProblemDetails { Status = status, Detail = error.Message };
        problem.Extensions["code"] = error.Code;
        foreach (var (key, value) in error.Extensions ?? new Dictionary<string, object?>())
        {
            problem.Extensions[key] = value;
        }

        return TypedResults.Problem(problem);
    }
}
