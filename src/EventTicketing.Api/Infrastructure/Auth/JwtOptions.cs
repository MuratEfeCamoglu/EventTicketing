using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EventTicketing.Api.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = "event-ticketing";

    [Required]
    public string Audience { get; set; } = "event-ticketing";

    /// <summary>≥ 32 bytes; from user-secrets or environment, never committed except as a <c>dev-only-</c> value (ISKELET §5.14).</summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
