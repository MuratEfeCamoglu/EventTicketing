using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;

namespace EventTicketing.Api.Features.Events;

public static class CreateEvent
{
    public sealed record Request(
        Guid? HallId,
        string Title,
        string? Description,
        EventCategory? Category,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt,
        IReadOnlyList<SectionPriceInput> SectionPrices) : IEventFields;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator(TimeProvider clock)
        {
            Include(new EventFieldsValidator());
            RuleFor(r => r.StartsAt).Must(startsAt => startsAt > clock.GetUtcNow()).When(r => r.StartsAt is not null)
                .WithMessage("The event must start in the future.");
        }
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<EventResponse>> HandleAsync(Guid organizerId, Request request, CancellationToken cancellationToken)
        {
            if (await db.ValidateHallAsync(request, cancellationToken) is { } error)
            {
                return error;
            }

            var created = new Event(
                organizerId,
                request.HallId!.Value,
                request.Title.Trim(),
                request.Description?.Trim() ?? string.Empty,
                request.Category!.Value,
                request.StartsAt!.Value.ToUniversalTime(),
                request.EndsAt!.Value.ToUniversalTime(),
                request.NormalizedPrices(),
                clock.GetUtcNow());
            db.Events.Add(created);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Event {EventId} created by {OrganizerId}", created.Id, organizerId);
            return EventResponse.From(created);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/events", async (Request request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(context.User.GetUserId(), request, cancellationToken))
                        .ToHttpResult(response => TypedResults.Created($"{EndpointRegistration.ApiPrefix}/events/{response.Id}", response)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
