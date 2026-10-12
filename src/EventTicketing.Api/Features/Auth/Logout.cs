using EventTicketing.Api.Common;
using EventTicketing.Api.Infrastructure.Auth;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Auth;

public static class Logout
{
    public sealed record Request(string RefreshToken);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.RefreshToken).NotEmpty();
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        /// <summary>Revokes the token if it is active. Always succeeds, so the response reveals nothing about the token (AC-02.4).</summary>
        public async Task HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var hash = TokenService.Hash(request.RefreshToken);
            var now = clock.GetUtcNow();
            await db.RefreshTokens
                .Where(t => t.TokenHash == hash && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/auth/logout", async (Request request, Handler handler, CancellationToken cancellationToken) =>
                {
                    await handler.HandleAsync(request, cancellationToken);
                    return TypedResults.NoContent();
                })
                .AddEndpointFilter<ValidationFilter<Request>>()
                .AllowAnonymous()
                .WithTags("Auth");
    }
}
