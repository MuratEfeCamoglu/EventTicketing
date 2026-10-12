using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>Public detail of a Published event; seat states live in <c>GET /events/{id}/seats</c> (AC-06.6).</summary>
public static class GetEvent
{
    public sealed record Response(
        Guid Id,
        string Title,
        string Description,
        EventCategory Category,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        EventStatus Status,
        VenueDto Venue,
        HallDto Hall,
        IReadOnlyList<SectionPriceResponse> SectionPrices);

    public sealed record VenueDto(Guid Id, string Name, string City, string Address);

    public sealed record HallDto(Guid Id, string Name);

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<Response>> HandleAsync(Guid eventId, CancellationToken cancellationToken)
        {
            var response = await (
                    from e in db.Events.AsNoTracking()
                    join h in db.Halls on e.HallId equals h.Id
                    join v in db.Venues on h.VenueId equals v.Id
                    where e.Id == eventId && e.Status == EventStatus.Published
                    select new Response(
                        e.Id,
                        e.Title,
                        e.Description,
                        e.Category,
                        e.StartsAt,
                        e.EndsAt,
                        e.Status,
                        new VenueDto(v.Id, v.Name, v.City, v.Address),
                        new HallDto(h.Id, h.Name),
                        e.SectionPrices.OrderBy(p => p.Section).Select(p => new SectionPriceResponse(p.Section, p.Price)).ToList()))
                .SingleOrDefaultAsync(cancellationToken);

            return response is null ? DomainErrors.NotFound : response;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/events/{id:guid}", async (Guid id, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AllowAnonymous()
                .WithTags("Events");
    }
}
