using System.Security.Claims;
using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>
/// An event of the caller in any status, with every field the edit form needs (AC-21.1). <c>GET /events/{id}</c> shows
/// only Published events; this is the owner's view. Another organizer's event is reported as not found.
/// </summary>
public static class GetOrganizerEvent
{
    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<GetEvent.Response>> HandleAsync(Guid eventId, ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            var userId = user.GetUserId();
            var isAdmin = user.IsAdmin();
            var response = await (
                    from e in db.Events.AsNoTracking()
                    join h in db.Halls on e.HallId equals h.Id
                    join v in db.Venues on h.VenueId equals v.Id
                    where e.Id == eventId && (isAdmin || e.OrganizerId == userId)
                    select new GetEvent.Response(
                        e.Id,
                        e.Title,
                        e.Description,
                        e.Category,
                        e.StartsAt,
                        e.EndsAt,
                        e.Status,
                        new GetEvent.VenueDto(v.Id, v.Name, v.City, v.Address),
                        new GetEvent.HallDto(h.Id, h.Name),
                        e.SectionPrices.OrderBy(p => p.Section).Select(p => new SectionPriceResponse(p.Section, p.Price)).ToList()))
                .SingleOrDefaultAsync(cancellationToken);

            return response is null ? DomainErrors.NotFound : response;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/organizer/events/{id:guid}", async (Guid id, Handler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, user, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
