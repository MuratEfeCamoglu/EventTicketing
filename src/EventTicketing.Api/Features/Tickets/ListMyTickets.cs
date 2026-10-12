using EventTicketing.Api.Common;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Tickets;

/// <summary>The caller's tickets with event, hall and seat label (<c>{section}-{row}-{number}</c>), newest first (AC-13.1).</summary>
public static class ListMyTickets
{
    public sealed record Request(int? Page, int? PageSize);

    public sealed record Item(
        Guid TicketId,
        string Code,
        Guid ReservationId,
        Guid EventId,
        string EventTitle,
        DateTimeOffset EventStartsAt,
        string HallName,
        string SeatLabel,
        DateTimeOffset IssuedAt);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => this.AddPagingRules(r => r.Page, r => r.PageSize);
    }

    public sealed class Handler(AppDbContext db)
    {
        public Task<PagedResponse<Item>> HandleAsync(Guid userId, Request request, CancellationToken cancellationToken) => (
                from t in db.Tickets.AsNoTracking()
                join es in db.EventSeats on t.EventSeatId equals es.Id
                join s in db.Seats on es.SeatId equals s.Id
                join e in db.Events on es.EventId equals e.Id
                join h in db.Halls on e.HallId equals h.Id
                where t.UserId == userId
                orderby t.IssuedAt descending, s.Section, s.Row, s.Number, t.Id
                select new Item(t.Id, t.Code, t.ReservationId, e.Id, e.Title, e.StartsAt, h.Name, s.Section + "-" + s.Row + "-" + s.Number.ToString(), t.IssuedAt))
            .ToPagedResponseAsync(request.Page, request.PageSize, cancellationToken);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/me/tickets", async ([AsParameters] Request request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await handler.HandleAsync(context.User.GetUserId(), request, cancellationToken)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Customer)
                .WithTags("Tickets");
    }
}
