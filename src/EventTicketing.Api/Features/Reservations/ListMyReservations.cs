using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Reservations;

/// <summary>The caller's reservations, newest first (AC-13.1).</summary>
public static class ListMyReservations
{
    public sealed record Request(int? Page, int? PageSize);

    public sealed record Item(
        Guid ReservationId,
        Guid EventId,
        string EventTitle,
        DateTimeOffset EventStartsAt,
        ReservationStatus Status,
        decimal TotalAmount,
        DateTimeOffset ExpiresAt,
        DateTimeOffset CreatedAt,
        int SeatCount);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => this.AddPagingRules(r => r.Page, r => r.PageSize);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public async Task<PagedResponse<Item>> HandleAsync(Guid userId, Request request, CancellationToken cancellationToken)
        {
            var page = await (
                    from r in db.Reservations.AsNoTracking()
                    join e in db.Events on r.EventId equals e.Id
                    where r.UserId == userId
                    orderby r.CreatedAt descending, r.Id descending
                    select new Item(r.Id, r.EventId, e.Title, e.StartsAt, r.Status, r.TotalAmount, r.ExpiresAt, r.CreatedAt, r.Seats.Count))
                .ToPagedResponseAsync(request.Page, request.PageSize, cancellationToken);

            var now = clock.GetUtcNow();
            return page with { Items = page.Items.Select(i => i with { Status = Reservation.EffectiveStatus(i.Status, i.ExpiresAt, now) }).ToList() };
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/me/reservations", async ([AsParameters] Request request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await handler.HandleAsync(context.User.GetUserId(), request, cancellationToken)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Customer)
                .WithTags("Reservations");
    }
}
