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

    /// <summary>
    /// AC-05.4: a Draft accepts every change; a Published event only a new title and description — any other field that
    /// differs from the current value is rejected. A Cancelled event cannot change.
    /// </summary>
    public Result Update(
        Guid hallId,
        string title,
        string description,
        EventCategory category,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        IReadOnlyCollection<(string Section, decimal Price)> sectionPrices,
        DateTimeOffset now)
    {
        if (Status == EventStatus.Cancelled)
        {
            return DomainErrors.Events.InvalidState;
        }

        if (Status == EventStatus.Published)
        {
            var current = _sectionPrices.ToDictionary(p => p.Section, p => p.Price);
            var pricesChanged = sectionPrices.Count != current.Count
                || sectionPrices.Any(p => !current.TryGetValue(p.Section, out var price) || price != p.Price);
            if (hallId != HallId || category != Category || startsAt != StartsAt || endsAt != EndsAt || pricesChanged)
            {
                return DomainErrors.Events.InvalidState;
            }
        }
        else
        {
            HallId = hallId;
            Category = category;
            StartsAt = startsAt;
            EndsAt = endsAt;
            ReplaceSectionPrices(sectionPrices);
        }

        Title = title;
        Description = description;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Published → Cancelled (AC-05.5). Releasing holds and checking sales is the caller's job.</summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (Status != EventStatus.Published)
        {
            return DomainErrors.Events.InvalidState;
        }

        Status = EventStatus.Cancelled;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Only the event's organizer or an Admin may change it (AC-03.2).</summary>
    public Result EnsureCanBeChangedBy(Guid userId, bool isAdmin) =>
        isAdmin || userId == OrganizerId ? Result.Success() : DomainErrors.Events.NotOwner;

    /// <summary>Only a Draft is physically deleted (AC-05.6, ISKELET §6).</summary>
    public Result EnsureDeletable() => Status == EventStatus.Draft ? Result.Success() : DomainErrors.Events.InvalidState;

    /// <summary>Updates prices in place, so EF Core sees updates instead of a delete and insert of the same key.</summary>
    private void ReplaceSectionPrices(IReadOnlyCollection<(string Section, decimal Price)> sectionPrices)
    {
        var requested = sectionPrices.ToDictionary(p => p.Section, p => p.Price);
        _sectionPrices.RemoveAll(p => !requested.ContainsKey(p.Section));
        foreach (var (section, price) in requested)
        {
            var existing = _sectionPrices.SingleOrDefault(p => p.Section == section);
            if (existing is null)
            {
                _sectionPrices.Add(new EventSectionPrice(Id, section, price));
            }
            else
            {
                existing.ChangePrice(price);
            }
        }
    }
}
