namespace EventTicketing.Api.Domain;

public sealed class EventSectionPrice
{
    private EventSectionPrice()
    {
    }

    internal EventSectionPrice(Guid eventId, string section, decimal price)
    {
        EventId = eventId;
        Section = section;
        Price = price;
    }

    public Guid EventId { get; private set; }

    public string Section { get; private set; } = null!;

    public decimal Price { get; private set; }
}
