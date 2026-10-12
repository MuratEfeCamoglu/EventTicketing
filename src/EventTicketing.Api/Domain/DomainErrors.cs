using EventTicketing.Api.Common;

namespace EventTicketing.Api.Domain;

public static class DomainErrors
{
    public static readonly Error NotFound = new(ErrorCodes.NotFound, "The resource was not found.");

    public static class Auth
    {
        public static readonly Error InvalidCredentials = new("auth.invalid_credentials", "E-mail or password is incorrect.");
        public static readonly Error RefreshTokenInvalid = new("auth.refresh_token_invalid", "The refresh token is not valid.");
        public static readonly Error RefreshTokenExpired = new("auth.refresh_token_expired", "The refresh token has expired.");
        public static readonly Error RefreshTokenReused = new("auth.refresh_token_reused", "The refresh token was already used; its session has been revoked.");
    }

    public static class Users
    {
        public static readonly Error EmailTaken = new("auth.email_taken", "The e-mail address is already registered.");
        public static readonly Error CannotAssignAdmin = new("user.cannot_assign_admin", "The Admin role cannot be assigned or removed through the API.");
    }

    public static class Events
    {
        public static readonly Error InvalidState = new("event.invalid_state", "The event's state does not allow this operation.");

        public static Error MissingSectionPrice(IEnumerable<string> sections) =>
            new("event.missing_section_price", $"No price is defined for section(s): {string.Join(", ", sections)}.");
    }
}
