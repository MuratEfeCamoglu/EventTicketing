using System.Security.Claims;
using EventTicketing.Api.Domain;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EventTicketing.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("The authenticated principal has no 'sub' claim."));

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(nameof(UserRole.Admin));
}
