using EventTicketing.Api.Common;

namespace EventTicketing.Api.Domain;

public static class DomainErrors
{
    public static readonly Error NotFound = new(ErrorCodes.NotFound, "The resource was not found.");

    /// <summary>A <c>400 validation.failed</c> found by the handler (needs the database), shaped like a validator error.</summary>
    public static Error Validation(string field, string message) =>
        new(ErrorCodes.ValidationFailed, message, new Dictionary<string, object?> { ["errors"] = new Dictionary<string, string[]> { [field] = [message] } });

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

    public static class Halls
    {
        public static readonly Error NameTaken = Validation("name", "The venue already has a hall with this name.");
    }

    public static class Events
    {
        public static readonly Error InvalidState = new("event.invalid_state", "The event's state does not allow this operation.");
        public static readonly Error NotOwner = new("event.not_owner", "Only the event's organizer can change it.");
        public static readonly Error HasSoldTickets = new("event.has_sold_tickets", "The event has sold tickets and cannot be cancelled.");
        public static readonly Error NotOnSale = new("event.not_on_sale", "The event is not on sale.");

        public static Error MissingSectionPrice(IEnumerable<string> sections) =>
            new("event.missing_section_price", $"No price is defined for section(s): {string.Join(", ", sections)}.");
    }

    public static class Seats
    {
        public static Error Unavailable(IReadOnlyList<Guid> eventSeatIds) =>
            new("seat.unavailable", "One or more seats are not available.", new Dictionary<string, object?> { ["unavailableSeatIds"] = eventSeatIds });

        public static readonly Error NotInEvent = new("seat.not_in_event", "One or more seats do not belong to the event.");
    }

    public static class Reservations
    {
        public static readonly Error InvalidState = new("reservation.invalid_state", "The reservation's state does not allow this operation.");
        public static readonly Error AlreadyPending = new("reservation.already_pending", "You already have a pending reservation for this event.");
    }
}
