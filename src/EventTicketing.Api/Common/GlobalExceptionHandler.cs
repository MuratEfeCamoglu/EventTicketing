using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Common;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string code;
        if (exception is DbUpdateConcurrencyException)
        {
            status = StatusCodes.Status409Conflict;
            code = ErrorCodes.ConcurrencyConflict;
            logger.LogWarning(exception, "Unhandled concurrency conflict on {RequestPath}", httpContext.Request.Path);
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            code = ErrorCodes.ServerError;
            logger.LogError(exception, "Unhandled exception on {RequestPath}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status };
        problem.Extensions["code"] = code;

        // Exception is deliberately not passed: the response must not carry exception details (ISKELET §5.12).
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }
}
