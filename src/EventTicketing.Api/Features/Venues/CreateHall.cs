using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Venues;

/// <summary>Creates a hall with its full seat layout in one request; the layout cannot change afterwards (AC-04.4).</summary>
public static class CreateHall
{
    public const int MinSeatsPerHall = 1;
    public const int MaxSeatsPerHall = 5000;

    public sealed record Request(string Name, IReadOnlyList<SectionInput> Sections);

    public sealed record SectionInput(string Name, IReadOnlyList<RowInput> Rows);

    public sealed record RowInput(string Label, int SeatCount);

    public sealed record Response(Guid Id, Guid VenueId, string Name, int SeatCount);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
            RuleFor(r => r.Sections).NotEmpty();
            RuleForEach(r => r.Sections).ChildRules(section =>
            {
                section.RuleFor(s => s.Name).NotEmpty().MaximumLength(50);
                section.RuleFor(s => s.Rows).NotEmpty();
                section.RuleForEach(s => s.Rows).ChildRules(row =>
                {
                    row.RuleFor(r => r.Label).NotEmpty().MaximumLength(10);
                    row.RuleFor(r => r.SeatCount).GreaterThanOrEqualTo(1);
                });
                section.RuleFor(s => s.Rows)
                    .Must(rows => HasNoDuplicates(rows.Select(r => r.Label)))
                    .When(s => s.Rows is not null)
                    .WithMessage("Row labels must be unique within a section.");
            });

            // AC-04.3: section names unique per hall, total seat count 1–5000.
            RuleFor(r => r.Sections)
                .Must(sections => HasNoDuplicates(sections.Select(s => s.Name)))
                .When(r => r.Sections is not null)
                .WithMessage("Section names must be unique within a hall.");
            RuleFor(r => r.Sections)
                .Must(sections => TotalSeats(sections) is >= MinSeatsPerHall and <= MaxSeatsPerHall)
                .When(r => r.Sections is not null)
                .WithMessage($"A hall must have between {MinSeatsPerHall} and {MaxSeatsPerHall} seats.");
        }

        private static bool HasNoDuplicates(IEnumerable<string?> values)
        {
            var names = values.Where(v => v is not null).Select(v => v!.Trim()).ToList();
            return names.Distinct(StringComparer.Ordinal).Count() == names.Count;
        }

        private static long TotalSeats(IEnumerable<SectionInput?> sections) =>
            sections.Where(s => s?.Rows is not null).SelectMany(s => s!.Rows).Where(r => r is not null).Sum(r => (long)r.SeatCount);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<Response>> HandleAsync(Guid venueId, Request request, CancellationToken cancellationToken)
        {
            if (!await db.Venues.AnyAsync(v => v.Id == venueId, cancellationToken))
            {
                return DomainErrors.NotFound;
            }

            var name = request.Name.Trim();
            if (await db.Halls.AnyAsync(h => h.VenueId == venueId && h.Name == name, cancellationToken))
            {
                return DomainErrors.Halls.NameTaken;
            }

            var hall = new Hall(venueId, name, clock.GetUtcNow());
            foreach (var section in request.Sections)
            {
                foreach (var row in section.Rows)
                {
                    hall.AddRow(section.Name.Trim(), row.Label.Trim(), row.SeatCount);
                }
            }

            db.Halls.Add(hall);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation("ux_halls_venue_id_name"))
            {
                // A concurrent request created a hall with the same name.
                return DomainErrors.Halls.NameTaken;
            }

            logger.LogInformation("Hall {HallId} with {SeatCount} seats created in venue {VenueId}", hall.Id, hall.Seats.Count, venueId);
            return new Response(hall.Id, hall.VenueId, hall.Name, hall.Seats.Count);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/venues/{id:guid}/halls", async (Guid id, Request request, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, request, cancellationToken))
                        .ToHttpResult(response => TypedResults.Created($"{EndpointRegistration.ApiPrefix}/halls/{response.Id}", response)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Admin)
                .WithTags("Venues");
    }
}
