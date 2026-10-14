using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Observability;
using EventTicketing.Api.Infrastructure.Persistence;
using EventTicketing.Api.Infrastructure.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Auth;

public static class Register
{
    public sealed record Request(string Email, string Password, string FullName);

    public sealed record Response(Guid Id, string Email, string FullName, UserRole Role);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;
            RuleFor(r => r.Email).NotEmpty().MaximumLength(256).EmailAddress();
            RuleFor(r => r.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Must(p => p.Any(char.IsLetter)).WithMessage("Password must contain at least one letter.")
                .Must(p => p.Any(char.IsDigit)).WithMessage("Password must contain at least one digit.");
            RuleFor(r => r.FullName).NotEmpty().MaximumLength(100);
        }
    }

    public sealed class Handler(AppDbContext db, IPasswordHasher<User> passwordHasher, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var email = User.NormalizeEmail(request.Email);
            if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                return DomainErrors.Users.EmailTaken;
            }

            var user = new User(email, request.FullName.Trim(), UserRole.Customer, clock.GetUtcNow());
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation("ux_users_email"))
            {
                // A concurrent registration with the same e-mail won the insert.
                return DomainErrors.Users.EmailTaken;
            }

            logger.LogInformation("User {UserId} registered with {Email}", user.Id, LogMasking.MaskEmail(email));
            return new Response(user.Id, user.Email, user.FullName, user.Role);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/auth/register", async (Request request, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(request, cancellationToken)).ToHttpResult(response => TypedResults.Created((string?)null, response)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .AllowAnonymous()
                .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
                .WithTags("Auth");
    }
}
