using EventTicketing.Api.Common;

namespace EventTicketing.Api.Domain;

public static class DomainErrors
{
    public static class Events
    {
        public static readonly Error InvalidState = new("event.invalid_state", "The event's state does not allow this operation.");

        public static Error MissingSectionPrice(IEnumerable<string> sections) =>
            new("event.missing_section_price", $"No price is defined for section(s): {string.Join(", ", sections)}.");
    }
}
