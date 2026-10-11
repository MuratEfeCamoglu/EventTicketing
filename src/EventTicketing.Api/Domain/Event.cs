using EventTicketing.Api.Common;

namespace EventTicketing.Api.Domain;

public sealed class Event
{
    private readonly List<EventSectionPrice> _sectionPrices = [];

    private Event()
    {
    }

    public Event(
        Guid organizerId,
        Guid hallId,
        string title,
        string description,
        EventCategory category,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        IEnumerable<(string Section, decimal Price)> sectionPrices,
        DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        OrganizerId = organizerId;
        HallId = hallId;
        Title = title;
        Description = description;
        Category = category;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Status = EventStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
        foreach (var (section, price) in sectionPrices)
        {
            _sectionPrices.Add(new EventSectionPrice(Id, section, price));
        }
    }

    public Guid Id { get; private set; }

    public Guid OrganizerId { get; private set; }

    public Guid HallId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public EventCategory Category { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public EventStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Mapped to PostgreSQL <c>xmin</c> (ISKELET §6).</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<EventSectionPrice> SectionPrices => _sectionPrices;

    /// <summary>
    /// Draft → Published (AC-05.3). Every section of the hall must have a price; returns one
    /// <see cref="EventSeat"/> per hall seat, priced by its section, for the caller to add.
    /// </summary>
    public Result<IReadOnlyList<EventSeat>> Publish(IReadOnlyCollection<Seat> hallSeats, DateTimeOffset now)
    {
        if (Status != EventStatus.Draft)
        {
            return DomainErrors.Events.InvalidState;
        }

        var prices = _sectionPrices.ToDictionary(p => p.Section, p => p.Price);
        var missing = hallSeats.Select(s => s.Section).Distinct().Where(s => !prices.ContainsKey(s)).Order().ToList();
        if (missing.Count > 0)
        {
            return DomainErrors.Events.MissingSectionPrice(missing);
        }

        Status = EventStatus.Published;
        UpdatedAt = now;
        return Result<IReadOnlyList<EventSeat>>.Success(hallSeats.Select(s => new EventSeat(Id, s.Id, prices[s.Section])).ToList());
    }
}
