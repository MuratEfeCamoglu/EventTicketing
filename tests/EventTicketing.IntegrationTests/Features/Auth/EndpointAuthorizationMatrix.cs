using EventTicketing.Api.Domain;

namespace EventTicketing.IntegrationTests.Features.Auth;

/// <summary>
/// ISKELET §5.13 as data (AC-03.1). Every endpoint the API maps must appear here, either as protected (with the roles
/// the policy admits) or as anonymous; <see cref="AuthorizationMatrixTests"/> enforces both the list and the behaviour.
/// </summary>
public static class EndpointAuthorizationMatrix
{
    public sealed record ProtectedEndpoint(string Method, string Route, IReadOnlyList<UserRole> AllowedRoles);

    public static readonly IReadOnlyList<ProtectedEndpoint> Protected =
    [
        new("PUT", "/api/v1/admin/users/{id}/role", [UserRole.Admin]),
    ];

    public static readonly IReadOnlyList<(string Method, string Route)> Anonymous =
    [
        ("POST", "/api/v1/auth/register"),
        ("POST", "/api/v1/auth/login"),
        ("POST", "/api/v1/auth/refresh"),
        ("POST", "/api/v1/auth/logout"),
    ];

    /// <summary>Route with every <c>{parameter}</c> replaced by a fresh id.</summary>
    public static string ToUrl(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, "{[^}]+}", _ => Guid.CreateVersion7().ToString());
}
