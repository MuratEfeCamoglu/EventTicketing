using System.Net;
using System.Text.RegularExpressions;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Tickets;

public sealed partial class TicketTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-11.1")]
    public async Task GetTicketQr_Owner_Returns200Png()
    {
        var ticketId = await BuyOneTicketAsync();

        var response = await Client.GetAsync($"/api/v1/tickets/{ticketId}/qr", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await response.Content.ReadAsByteArrayAsync(Ct)).Take(PngSignature.Length).ShouldBe(PngSignature);
    }

    [Fact]
    [Trait("AC", "AC-11.1")]
    public async Task GetTicketQr_Admin_Returns200Png()
    {
        var ticketId = await BuyOneTicketAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.GetAsync($"/api/v1/tickets/{ticketId}/qr", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(Ct)).Take(PngSignature.Length).ShouldBe(PngSignature);
    }

    [Fact]
    [Trait("AC", "AC-11.2")]
    public async Task IssuedTickets_HaveUniqueCrockfordCodes()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.BuyAsync(eventId, [.. seatIds.Take(6)]);

        var codes = await WithDbAsync(db => db.Tickets.Select(t => t.Code).ToListAsync(Ct));

        codes.Count.ShouldBe(6);
        codes.Distinct().Count().ShouldBe(6);
        codes.ShouldAllBe(c => CrockfordCode().IsMatch(c));
    }

    [Fact]
    [Trait("AC", "AC-11.3")]
    public async Task GetTicketQr_OtherCustomersTicket_Returns404()
    {
        var ticketId = await BuyOneTicketAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        await (await Client.GetAsync($"/api/v1/tickets/{ticketId}/qr", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
        await (await Client.GetAsync($"/api/v1/tickets/{Guid.CreateVersion7()}/qr", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-13.1")]
    public async Task ListMyTickets_ReturnsOnlyCallersTicketsWithEventAndSeat()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.BuyAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.BuyAsync(eventId, seatIds[^1], seatIds[^2]);

        var response = await Client.GetAsync("/api/v1/me/tickets", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("totalCount").GetInt32().ShouldBe(2);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("seatLabel").GetString()).ShouldBe(["Floor-B-4", "Floor-B-5"]);
        items[0].GetProperty("eventId").GetGuid().ShouldBe(eventId);
        items[0].GetProperty("eventTitle").GetString().ShouldNotBeNullOrEmpty();
        items[0].GetProperty("hallName").GetString().ShouldBe("Main Hall");
        items[0].TryGetProperty("eventStartsAt", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-13.1")]
    public async Task ListMyTickets_Paged_RespectsPageSize()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.BuyAsync(eventId, seatIds[0], seatIds[1], seatIds[2]);

        var body = await (await Client.GetAsync("/api/v1/me/tickets?page=2&pageSize=2", Ct)).ReadJsonAsync();

        body.GetProperty("totalPages").GetInt32().ShouldBe(2);
        body.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    private async Task<Guid> BuyOneTicketAsync()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.BuyAsync(eventId, seatIds[0]);
        return await WithDbAsync(db => db.Tickets.Where(t => t.ReservationId == reservationId).Select(t => t.Id).SingleAsync(Ct));
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$")]
    private static partial Regex CrockfordCode();
}
