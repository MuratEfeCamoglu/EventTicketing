using System.Text;
using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventTicketing.Api.Infrastructure.Auth;

public static class AuthSetup
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static WebApplicationBuilder AddAuth(this WebApplicationBuilder builder)
    {
        var isProduction = builder.Environment.IsProduction();

        builder.Services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinimumSigningKeyBytes, $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyBytes} bytes.")
            .Validate(o => !isProduction || !DevOnlyValues.Contains(o.SigningKey), DevOnlyValues.RejectionMessage)
            .ValidateOnStart();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwtOptions, clock) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                    ClockSkew = ClockSkew,

                    // nbf/exp are checked against TimeProvider, so tokens stay valid when tests advance FakeTimeProvider (ISKELET §5.11).
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                    {
                        var now = clock.GetUtcNow().UtcDateTime;
                        return (notBefore is null || notBefore.Value <= now + parameters.ClockSkew)
                            && expires is not null
                            && now < expires.Value + parameters.ClockSkew;
                    },
                };
            });

        builder.Services.AddAuthorizationBuilder().AddEventTicketingPolicies();
        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddSingleton<TokenService>();

        return builder;
    }
}
