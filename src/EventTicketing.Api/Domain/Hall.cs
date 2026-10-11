namespace EventTicketing.Api.Domain;

/// <summary>A hall's seat layout is fixed once created (AC-04.4); seats are only added while building it.</summary>
public sealed class Hall
{
    private readonly List<Seat> _seats = [];

    private Hall()
    {
    }

    public Hall(Guid venueId, string name, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        VenueId = venueId;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid VenueId { get; private set; }

    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Seat> Seats => _seats;

    /// <summary>Adds a row; seat numbers start at 1 in every row (AC-04.2).</summary>
    public void AddRow(string section, string row, int seatCount)
    {
        for (var number = 1; number <= seatCount; number++)
        {
            _seats.Add(new Seat(Id, section, row, number));
        }
    }
}
