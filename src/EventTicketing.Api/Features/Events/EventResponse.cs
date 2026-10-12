using EventTicketing.Api.Domain;

namespace EventTicketing.Api.Features.Events;

/// <summary>The event as its organizer sees it; returned by the write endpoints of F-05.</summary>
public sealed record EventResponse(
    Guid Id,
    Guid OrganizerId,
    Guid HallId,
    string Title,
    string Description,
    EventCategory Category,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    EventStatus Status,
    IReadOnlyList<SectionPriceResponse> SectionPrices)
{
    public static EventResponse From(Event e) => new(
        e.Id,
        e.OrganizerId,
        e.HallId,
        e.Title,
        e.Description,
        e.Category,
        e.StartsAt,
        e.EndsAt,
        e.Status,
        e.SectionPrices.OrderBy(p => p.Section, StringComparer.Ordinal).Select(p => new SectionPriceResponse(p.Section, p.Price)).ToList());
}

public sealed record SectionPriceResponse(string Section, decimal Price);
