using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Users;

public static class ChangeRole
{
    public sealed record Request(UserRole? Role);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.Role).NotNull().IsInEnum();
    }

    public sealed class Handler(AppDbContext db, ILogger<Handler> logger)
    {
        public async Task<Result> HandleAsync(Guid userId, Request request, CancellationToken cancellationToken)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null)
            {
                return DomainErrors.NotFound;
            }

            var result = user.ChangeRole(request.Role!.Value);
            if (!result.IsSuccess)
            {
                return result;
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("User {UserId} role changed to {Role}", user.Id, user.Role);
            return result;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPut("/admin/users/{id:guid}/role", async (Guid id, Request request, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, request, cancellationToken)).ToHttpResult(TypedResults.NoContent))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Admin)
                .WithTags("Users");
    }
}
