namespace EventTicketing.Api.Common;

/// <summary>Error code → HTTP status table from ISKELET §5.12. A new code is added to the table first.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string Unauthorized = "auth.unauthorized";
    public const string Forbidden = "auth.forbidden";
    public const string NotFound = "resource.not_found";
    public const string ConcurrencyConflict = "concurrency.conflict";
    public const string RateLimitExceeded = "rate_limit.exceeded";
    public const string ServerError = "server.error";

    public static readonly IReadOnlyDictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        [ValidationFailed] = StatusCodes.Status400BadRequest,
        ["seat.not_in_event"] = StatusCodes.Status400BadRequest,
        ["event.missing_section_price"] = StatusCodes.Status400BadRequest,
        ["user.cannot_assign_admin"] = StatusCodes.Status400BadRequest,
        ["auth.invalid_credentials"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_invalid"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_expired"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_reused"] = StatusCodes.Status401Unauthorized,
        [Unauthorized] = StatusCodes.Status401Unauthorized,
        ["payment.declined"] = StatusCodes.Status402PaymentRequired,
        [Forbidden] = StatusCodes.Status403Forbidden,
        ["event.not_owner"] = StatusCodes.Status403Forbidden,
        [NotFound] = StatusCodes.Status404NotFound,
        ["auth.email_taken"] = StatusCodes.Status409Conflict,
        ["event.invalid_state"] = StatusCodes.Status409Conflict,
        ["event.has_sold_tickets"] = StatusCodes.Status409Conflict,
        ["event.not_on_sale"] = StatusCodes.Status409Conflict,
        ["seat.unavailable"] = StatusCodes.Status409Conflict,
        ["reservation.already_pending"] = StatusCodes.Status409Conflict,
        ["reservation.expired"] = StatusCodes.Status409Conflict,
        ["reservation.invalid_state"] = StatusCodes.Status409Conflict,
        [ConcurrencyConflict] = StatusCodes.Status409Conflict,
        ["payment.idempotency_key_reused"] = StatusCodes.Status422UnprocessableEntity,
        [RateLimitExceeded] = StatusCodes.Status429TooManyRequests,
        [ServerError] = StatusCodes.Status500InternalServerError,
    };

    /// <summary>Code used when a response is produced without an explicit <see cref="Error"/> (status code pages, framework 4xx).</summary>
    public static readonly IReadOnlyDictionary<int, string> DefaultCodeByStatus = new Dictionary<int, string>
    {
        [StatusCodes.Status400BadRequest] = ValidationFailed,
        [StatusCodes.Status401Unauthorized] = Unauthorized,
        [StatusCodes.Status403Forbidden] = Forbidden,
        [StatusCodes.Status404NotFound] = NotFound,
        [StatusCodes.Status429TooManyRequests] = RateLimitExceeded,
        [StatusCodes.Status500InternalServerError] = ServerError,
    };
}
