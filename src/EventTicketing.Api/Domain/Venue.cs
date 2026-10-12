namespace EventTicketing.Api.Domain;

public sealed class Venue
{
    private Venue()
    {
    }

    public Venue(string name, string city, string address, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        Name = name;
        City = city;
        Address = address;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string City { get; private set; } = null!;

    public string Address { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Only the descriptive fields change; halls and seat layouts are fixed (AC-04.4).</summary>
    public void Update(string name, string city, string address)
    {
        Name = name;
        City = city;
        Address = address;
    }
}
