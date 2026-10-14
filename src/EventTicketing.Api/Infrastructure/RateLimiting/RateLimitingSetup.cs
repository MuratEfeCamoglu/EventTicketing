using System.Globalization;
using System.Threading.RateLimiting;
using EventTicketing.Api.Common;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Infrastructure.RateLimiting;

/// <summary><c>RateLimiting:*</c> keys (ISKELET §5.14); false in integration tests and k6 runs.</summary>
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public bool Enabled { get; set; } = true;
}

/// <summary>Fixed one-minute windows of AC-20.1 (bonus F-20), built on ASP.NET Core rate limiting.</summary>
public static class RateLimitingSetup
{
    /// <summary><c>/auth/login</c> and <c>/auth/register</c>: per client IP.</summary>
    public const string AuthPolicy = "auth";

    /// <summary><c>POST /events/{id}/reservations</c>: per authenticated user.</summary>
    public const string ReservationsPolicy = "reservations";

    public const int AuthPermitsPerWindow = 10;
    public const int ReservationPermitsPerWindow = 30;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static WebApplicationBuilder AddEventTicketingRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.Section)
            .ValidateOnStart();

        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => FixedWindow(AuthPermitsPerWindow)));

            options.AddPolicy(ReservationsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.IsAuthenticated == true ? context.User.GetUserId().ToString() : "anonymous",
                _ => FixedWindow(ReservationPermitsPerWindow)));

            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                var retryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : Window;
                response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await new Error(ErrorCodes.RateLimitExceeded, "Too many requests; retry after the time in the Retry-After header.")
                    .ToProblem()
                    .ExecuteAsync(rejected.HttpContext);
            };
        });

        return builder;
    }

    /// <summary>Adds the middleware only when <c>RateLimiting:Enabled</c> is true; otherwise the endpoint policies are inert.</summary>
    public static WebApplication UseEventTicketingRateLimiting(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Enabled)
        {
            app.UseRateLimiter();
        }

        return app;
    }

    private static FixedWindowRateLimiterOptions FixedWindow(int permits) => new()
    {
        PermitLimit = permits,
        Window = Window,
        QueueLimit = 0,
    };
}
