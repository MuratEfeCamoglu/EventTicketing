using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Venues;

public static class GetHall
{
    public sealed record Response(Guid Id, Guid VenueId, string Name, IReadOnlyList<SectionDto> Sections);

    public sealed record SectionDto(string Name, IReadOnlyList<RowDto> Rows);

    public sealed record RowDto(string Label, IReadOnlyList<SeatDto> Seats);

    public sealed record SeatDto(Guid Id, int Number);

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<Response>> HandleAsync(Guid hallId, CancellationToken cancellationToken)
        {
            var hall = await db.Halls.AsNoTracking()
                .Where(h => h.Id == hallId)
                .Select(h => new { h.Id, h.VenueId, h.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (hall is null)
            {
                return DomainErrors.NotFound;
            }

            var seats = await db.Seats.AsNoTracking()
                .Where(s => s.HallId == hallId)
                .OrderBy(s => s.Section).ThenBy(s => s.Row).ThenBy(s => s.Number)
                .Select(s => new { s.Id, s.Section, s.Row, s.Number })
                .ToListAsync(cancellationToken);

            var sections = seats
                .GroupBy(s => s.Section)
                .Select(section => new SectionDto(
                    section.Key,
                    section.GroupBy(s => s.Row)
                        .Select(row => new RowDto(row.Key, row.Select(s => new SeatDto(s.Id, s.Number)).ToList()))
                        .ToList()))
                .ToList();

            return new Response(hall.Id, hall.VenueId, hall.Name, sections);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/halls/{id:guid}", async (Guid id, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AllowAnonymous()
                .WithTags("Venues");
    }
}
