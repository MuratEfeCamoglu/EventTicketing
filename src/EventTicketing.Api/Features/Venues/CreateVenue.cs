using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;

namespace EventTicketing.Api.Features.Venues;

public static class CreateVenue
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

    public sealed class Handler(AppDbContext db, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var venue = new Venue(request.Name.Trim(), request.City.Trim(), request.Address.Trim(), clock.GetUtcNow());
            db.Venues.Add(venue);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Venue {VenueId} created", venue.Id);
            return new Response(venue.Id, venue.Name, venue.City, venue.Address);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/venues", async (Request request, Handler handler, CancellationToken cancellationToken) =>
                {
                    var response = await handler.HandleAsync(request, cancellationToken);
                    return TypedResults.Created($"{EndpointRegistration.ApiPrefix}/venues/{response.Id}", response);
                })
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Admin)
                .WithTags("Venues");
    }
}
