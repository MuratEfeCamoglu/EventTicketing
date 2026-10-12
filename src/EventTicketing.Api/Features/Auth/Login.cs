using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Auth;
using EventTicketing.Api.Infrastructure.Observability;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Auth;

public static class Login
{
    public sealed record Request(string Email, string Password);

    public sealed record Response(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Email).NotEmpty();
            RuleFor(r => r.Password).NotEmpty();
        }
    }

    public sealed class Handler(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        TokenService tokens,
        ILogger<Handler> logger)
    {
        // An unknown e-mail still pays for one hash verification, so it is not cheaper to probe than a wrong password.
        private static readonly User UnknownUser = new("unknown@invalid", "unknown", UserRole.Customer, DateTimeOffset.UnixEpoch);
        private static readonly Lazy<string> UnknownUserHash = new(() => new PasswordHasher<User>().HashPassword(UnknownUser, Guid.NewGuid().ToString()));

        public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var email = User.NormalizeEmail(request.Email);
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

            var verification = user is null
                ? passwordHasher.VerifyHashedPassword(UnknownUser, UnknownUserHash.Value, request.Password)
                : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (user is null || verification == PasswordVerificationResult.Failed)
            {
                logger.LogInformation("Login failed for {Email}", LogMasking.MaskEmail(email));
                return DomainErrors.Auth.InvalidCredentials;
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
            }

            var issued = tokens.Issue(user, familyId: Guid.CreateVersion7());
            db.RefreshTokens.Add(issued.RefreshTokenEntity);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("User {UserId} logged in", user.Id);
            return new Response(issued.AccessToken, issued.AccessTokenExpiresAt, issued.RefreshToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/auth/login", async (Request request, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(request, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .AllowAnonymous()
                .WithTags("Auth");
    }
}
