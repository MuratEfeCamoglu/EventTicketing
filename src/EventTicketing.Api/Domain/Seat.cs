namespace EventTicketing.Api.Domain;

public sealed class Seat
{
    private Seat()
    {
    }

    internal Seat(Guid hallId, string section, string row, int number)
    {
        Id = Guid.CreateVersion7();
        HallId = hallId;
        Section = section;
        Row = row;
        Number = number;
    }

    public Guid Id { get; private set; }

    public Guid HallId { get; private set; }

    public string Section { get; private set; } = null!;

    public string Row { get; private set; } = null!;

    public int Number { get; private set; }
}
