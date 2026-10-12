using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using EventTicketing.Api.Infrastructure.Qr;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Tickets;

/// <summary>PNG QR code of <c>ETKT:{ticketCode}</c> for the ticket's owner or an Admin (F-11).</summary>
public static class GetTicketQr
{
    public const string PngContentType = "image/png";

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<byte[]>> HandleAsync(Guid ticketId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
        {
            var code = await db.Tickets.AsNoTracking()
                .Where(t => t.Id == ticketId && (isAdmin || t.UserId == userId))
                .Select(t => t.Code)
                .SingleOrDefaultAsync(cancellationToken);

            return code is null ? DomainErrors.NotFound : QrCodeGenerator.GeneratePng(Ticket.QrContent(code));
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/tickets/{id:guid}/qr", async (Guid id, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), context.User.IsAdmin(), cancellationToken))
                        .ToHttpResult(png => TypedResults.File(png, PngContentType)))
                .RequireAuthorization(Policies.CustomerOrAdmin)
                .WithTags("Tickets");
    }
}
