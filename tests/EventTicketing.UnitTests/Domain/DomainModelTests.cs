using EventTicketing.Api.Domain;

namespace EventTicketing.UnitTests.Domain;

public sealed class DomainModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void User_Create_NormalizesEmail()
    {
        var user = new User("  Ayse.Yilmaz@Example.COM ", "Ayşe", UserRole.Customer, Now);

        user.Email.ShouldBe("ayse.yilmaz@example.com");
    }

    [Fact]
    public void Hall_AddRow_NumbersSeatsFromOnePerRow()
    {
        var hall = new Hall(Guid.CreateVersion7(), "Hall", Now);

        hall.AddRow("Floor", "A", 3);
        hall.AddRow("Floor", "B", 2);

        hall.Seats.Count.ShouldBe(5);
        hall.Seats.Where(s => s.Row == "A").Select(s => s.Number).ShouldBe([1, 2, 3]);
        hall.Seats.Where(s => s.Row == "B").Select(s => s.Number).ShouldBe([1, 2]);
        hall.Seats.ShouldAllBe(s => s.HallId == hall.Id);
    }

    [Fact]
    public void Event_Publish_Draft_CreatesAvailableSeatPerHallSeatWithSectionPrice()
    {
        var hall = HallWithSections("Floor", "Balcony");
        var @event = DraftEvent(hall, ("Floor", 750m), ("Balcony", 450m));

        var result = @event.Publish(hall.Seats, Now.AddMinutes(1));

        result.IsSuccess.ShouldBeTrue();
        @event.Status.ShouldBe(EventStatus.Published);
        @event.UpdatedAt.ShouldBe(Now.AddMinutes(1));
        result.Value.Count.ShouldBe(hall.Seats.Count);
        result.Value.ShouldAllBe(s => s.Status == EventSeatStatus.Available && s.EventId == @event.Id);
        var floorSeatIds = hall.Seats.Where(s => s.Section == "Floor").Select(s => s.Id).ToHashSet();
        result.Value.Where(s => floorSeatIds.Contains(s.SeatId)).ShouldAllBe(s => s.Price == 750m);
        result.Value.Where(s => !floorSeatIds.Contains(s.SeatId)).ShouldAllBe(s => s.Price == 450m);
    }

    [Fact]
    public void Event_Publish_SectionWithoutPrice_ReturnsMissingSectionPriceAndStaysDraft()
    {
        var hall = HallWithSections("Floor", "Balcony");
        var @event = DraftEvent(hall, ("Floor", 750m));

        var result = @event.Publish(hall.Seats, Now);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("event.missing_section_price");
        result.Error.Message.ShouldContain("Balcony");
        @event.Status.ShouldBe(EventStatus.Draft);
    }

    [Fact]
    public void Event_Publish_AlreadyPublished_ReturnsInvalidState()
    {
        var hall = HallWithSections("Floor");
        var @event = DraftEvent(hall, ("Floor", 100m));
        @event.Publish(hall.Seats, Now);

        var result = @event.Publish(hall.Seats, Now);

        result.Error!.Code.ShouldBe("event.invalid_state");
    }

    [Fact]
    public void Reservation_Create_IsPendingWithTotalOfSeatPrices()
    {
        var hall = HallWithSections("Floor", "Balcony");
        var @event = DraftEvent(hall, ("Floor", 750.50m), ("Balcony", 450.25m));
        var seats = @event.Publish(hall.Seats, Now).Value;
        var chosen = new[] { seats[0], seats[^1] };

        var reservation = new Reservation(Guid.CreateVersion7(), @event.Id, chosen, Now.AddMinutes(10), Now);

        reservation.Status.ShouldBe(ReservationStatus.Pending);
        reservation.TotalAmount.ShouldBe(chosen.Sum(s => s.Price));
        reservation.Seats.Select(s => s.EventSeatId).ShouldBe(chosen.Select(s => s.Id));
        reservation.ExpiresAt.ShouldBe(Now.AddMinutes(10));
    }

    private static Hall HallWithSections(params string[] sections)
    {
        var hall = new Hall(Guid.CreateVersion7(), "Hall", Now);
        foreach (var section in sections)
        {
            hall.AddRow(section, "A", 2);
        }

        return hall;
    }

    private static Event DraftEvent(Hall hall, params (string Section, decimal Price)[] prices) =>
        new(Guid.CreateVersion7(), hall.Id, "Title", "", EventCategory.Concert, Now.AddDays(1), Now.AddDays(1).AddHours(2), prices, Now);
}
