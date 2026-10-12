using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>Public catalogue: Published events that have not started yet (F-06).</summary>
public static class ListEvents
{
    public const string SortByStartsAt = "startsAt";
    public const string SortByStartsAtDescending = "-startsAt";
    public const string SortByTitle = "title";

    private static readonly string[] SortValues = [SortByStartsAt, SortByStartsAtDescending, SortByTitle];

    public sealed record Request(string? City, string? Category, DateTimeOffset? From, DateTimeOffset? To, string? Q, string? Sort, int? Page, int? PageSize);

    public sealed record Item(Guid Id, string Title, EventCategory Category, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string VenueName, string City, string HallName, decimal? MinPrice);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            this.AddPagingRules(r => r.Page, r => r.PageSize);
            RuleFor(r => r.Category)
                .Must(c => Enum.GetNames<EventCategory>().Contains(c, StringComparer.OrdinalIgnoreCase))
                .When(r => r.Category is not null)
                .WithMessage($"Category must be one of: {string.Join(", ", Enum.GetNames<EventCategory>())}.");
            RuleFor(r => r.Sort)
                .Must(s => SortValues.Contains(s, StringComparer.Ordinal))
                .When(r => r.Sort is not null)
                .WithMessage($"Sort must be one of: {string.Join(", ", SortValues)}.");
        }
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public Task<PagedResponse<Item>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            var events =
                from e in db.Events.AsNoTracking()
                join h in db.Halls on e.HallId equals h.Id
                join v in db.Venues on h.VenueId equals v.Id
                where e.Status == EventStatus.Published && e.StartsAt > now
                select new { Event = e, HallName = h.Name, VenueName = v.Name, v.City };

            if (!string.IsNullOrWhiteSpace(request.City))
            {
                var city = LikePattern.Exact(request.City);
                events = events.Where(x => EF.Functions.ILike(x.City, city, LikePattern.EscapeCharacter));
            }

            if (request.Category is not null)
            {
                var category = Enum.Parse<EventCategory>(request.Category, ignoreCase: true);
                events = events.Where(x => x.Event.Category == category);
            }

            if (request.From is { } from)
            {
                var fromUtc = from.ToUniversalTime();
                events = events.Where(x => x.Event.StartsAt >= fromUtc);
            }

            if (request.To is { } to)
            {
                var toUtc = to.ToUniversalTime();
                events = events.Where(x => x.Event.StartsAt <= toUtc);
            }

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var pattern = LikePattern.Contains(request.Q);
                events = events.Where(x => EF.Functions.ILike(x.Event.Title, pattern, LikePattern.EscapeCharacter));
            }

            // The id tie-breaker keeps pages stable: no row repeats or goes missing between pages (AC-06.4).
            var ordered = (request.Sort ?? SortByStartsAt) switch
            {
                SortByStartsAtDescending => events.OrderByDescending(x => x.Event.StartsAt).ThenBy(x => x.Event.Id),
                SortByTitle => events.OrderBy(x => x.Event.Title).ThenBy(x => x.Event.Id),
                _ => events.OrderBy(x => x.Event.StartsAt).ThenBy(x => x.Event.Id),
            };

            return ordered
                .Select(x => new Item(
                    x.Event.Id,
                    x.Event.Title,
                    x.Event.Category,
                    x.Event.StartsAt,
                    x.Event.EndsAt,
                    x.VenueName,
                    x.City,
                    x.HallName,
                    x.Event.SectionPrices.Min(p => (decimal?)p.Price)))
                .ToPagedResponseAsync(request.Page, request.PageSize, cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/events", async ([AsParameters] Request request, Handler handler, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await handler.HandleAsync(request, cancellationToken)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .AllowAnonymous()
                .WithTags("Events");
    }
}
