using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Venues;

public static class GetVenue
{
    public sealed record Response(Guid Id, string Name, string City, string Address, IReadOnlyList<HallSummary> Halls);

    public sealed record HallSummary(Guid Id, string Name, int SeatCount);

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<Response>> HandleAsync(Guid venueId, CancellationToken cancellationToken)
        {
            var venue = await db.Venues.AsNoTracking()
                .Where(v => v.Id == venueId)
                .Select(v => new { v.Id, v.Name, v.City, v.Address })
                .SingleOrDefaultAsync(cancellationToken);
            if (venue is null)
            {
                return DomainErrors.NotFound;
            }

            var halls = await db.Halls.AsNoTracking()
                .Where(h => h.VenueId == venueId)
                .OrderBy(h => h.Name).ThenBy(h => h.Id)
                .Select(h => new HallSummary(h.Id, h.Name, h.Seats.Count))
                .ToListAsync(cancellationToken);

            return new Response(venue.Id, venue.Name, venue.City, venue.Address, halls);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/venues/{id:guid}", async (Guid id, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AllowAnonymous()
                .WithTags("Venues");
    }
}
