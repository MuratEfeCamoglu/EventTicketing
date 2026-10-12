using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Venues;

/// <summary>Updates only <c>name, city, address</c>; there is no endpoint that changes a seat layout (AC-04.4).</summary>
public static class UpdateVenue
{
    public sealed record Request(string Name, string City, string Address);

    public sealed record Response(Guid Id, string Name, string City, string Address);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
            RuleFor(r => r.City).NotEmpty().MaximumLength(100);
            RuleFor(r => r.Address).NotEmpty().MaximumLength(500);
        }
    }

    public sealed class Handler(AppDbContext db, ILogger<Handler> logger)
    {
        public async Task<Result<Response>> HandleAsync(Guid venueId, Request request, CancellationToken cancellationToken)
        {
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == venueId, cancellationToken);
            if (venue is null)
            {
                return DomainErrors.NotFound;
            }

            venue.Update(request.Name.Trim(), request.City.Trim(), request.Address.Trim());
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Venue {VenueId} updated", venue.Id);
            return new Response(venue.Id, venue.Name, venue.City, venue.Address);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPut("/venues/{id:guid}", async (Guid id, Request request, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, request, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Admin)
                .WithTags("Venues");
    }
}
