using System.Diagnostics;

namespace EventTicketing.Api.Common;

/// <summary>Adds the ISKELET §5.12 extension fields (<c>code</c>, <c>traceId</c>) to every ProblemDetails response.</summary>
public static class ProblemDetailsSetup
{
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        // W3C trace id: the same value Serilog writes as TraceId on the request log line (AC-18.1).
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

        if (!problem.Extensions.ContainsKey("code")
            && problem.Status is { } status
            && ErrorCodes.DefaultCodeByStatus.TryGetValue(status, out var code))
        {
            problem.Extensions["code"] = code;
        }
    }
}
