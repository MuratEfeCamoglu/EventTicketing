using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Auth;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Auth;

/// <summary>
/// Refresh token rotation (ISKELET §5.11): every use issues a new token in the same family and revokes the old one.
/// A revoked token coming back means it leaked, so the whole family is revoked (AC-02.2).
/// </summary>
public static class Refresh
{
    public sealed record Request(string RefreshToken);

    public sealed record Response(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.RefreshToken).NotEmpty();
    }

    public sealed class Handler(AppDbContext db, TokenService tokens, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var hash = TokenService.Hash(request.RefreshToken);
            var current = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
            if (current is null)
            {
                return DomainErrors.Auth.RefreshTokenInvalid;
            }

            var now = clock.GetUtcNow();
            if (current.RevokedAt is not null)
            {
                return await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            }

            if (current.ExpiresAt <= now)
            {
                return DomainErrors.Auth.RefreshTokenExpired;
            }

            // The role is read again, so a role change applies from the next access token (AC-03.4).
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == current.UserId, cancellationToken);
            var issued = tokens.Issue(user, current.FamilyId);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.RefreshTokens.Add(issued.RefreshTokenEntity);
            await db.SaveChangesAsync(cancellationToken);

            // Conditional revoke: of two concurrent refreshes with the same token, only one sees revoked_at IS NULL.
            var revoked = await db.RefreshTokens
                .Where(t => t.Id == current.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.ReplacedByTokenId, issued.RefreshTokenEntity.Id),
                    cancellationToken);
            if (revoked == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new Response(issued.AccessToken, issued.AccessTokenExpiresAt, issued.RefreshToken);
        }

        private async Task<Error> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var revoked = await db.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
            logger.LogWarning("Refresh token reuse detected; revoked {TokenCount} tokens of family {FamilyId}", revoked, familyId);
            return DomainErrors.Auth.RefreshTokenReused;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/auth/refresh", async (Request request, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(request, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .AllowAnonymous()
                .WithTags("Auth");
    }
}
