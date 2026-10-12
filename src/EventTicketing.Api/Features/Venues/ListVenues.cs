using EventTicketing.Api.Common;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Venues;

public static class ListVenues
{
    public sealed record Request(string? City, int? Page, int? PageSize);

    public sealed record Item(Guid Id, string Name, string City, string Address);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => this.AddPagingRules(r => r.Page, r => r.PageSize);
    }

    public sealed class Handler(AppDbContext db)
    {
        public Task<PagedResponse<Item>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var venues = db.Venues.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.City))
            {
                // City matches case-insensitively and in full.
                var city = request.City.Trim().ToLower();
                venues = venues.Where(v => v.City.ToLower() == city);
            }

            return venues
                .OrderBy(v => v.Name).ThenBy(v => v.Id)
                .Select(v => new Item(v.Id, v.Name, v.City, v.Address))
                .ToPagedResponseAsync(request.Page, request.PageSize, cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/venues", async ([AsParameters] Request request, Handler handler, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await handler.HandleAsync(request, cancellationToken)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .AllowAnonymous()
                .WithTags("Venues");
    }
}
