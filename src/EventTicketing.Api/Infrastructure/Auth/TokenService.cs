using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using EventTicketing.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventTicketing.Api.Infrastructure.Auth;

public sealed record IssuedTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, RefreshToken RefreshTokenEntity);

/// <summary>Issues access tokens (JWT HS256) and refresh tokens (ISKELET §5.11). All times come from <see cref="TimeProvider"/>.</summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private const int RefreshTokenBytes = 64;
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <summary>Issues a new access token and a refresh token in <paramref name="familyId"/>. The caller stores the refresh token entity.</summary>
    public IssuedTokens Issue(User user, Guid familyId)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var accessTokenExpiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var accessToken = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessTokenExpiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                ["role"] = user.Role.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        var entity = new RefreshToken(user.Id, Hash(refreshToken), familyId, now.AddDays(jwt.RefreshTokenDays), now);

        return new IssuedTokens(accessToken, accessTokenExpiresAt, refreshToken, entity);
    }

    /// <summary>SHA-256 as 64 lowercase hex characters; only this value is stored (AC-02.5).</summary>
    public static string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
