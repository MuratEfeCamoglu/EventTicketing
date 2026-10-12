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
        new("POST", "/api/v1/venues", [UserRole.Admin]),
        new("PUT", "/api/v1/venues/{id}", [UserRole.Admin]),
        new("POST", "/api/v1/venues/{id}/halls", [UserRole.Admin]),
        new("POST", "/api/v1/events", [UserRole.Organizer, UserRole.Admin]),
        new("PUT", "/api/v1/events/{id}", [UserRole.Organizer, UserRole.Admin]),
        new("POST", "/api/v1/events/{id}/publish", [UserRole.Organizer, UserRole.Admin]),
        new("POST", "/api/v1/events/{id}/cancel", [UserRole.Organizer, UserRole.Admin]),
        new("DELETE", "/api/v1/events/{id}", [UserRole.Organizer, UserRole.Admin]),
        new("GET", "/api/v1/organizer/events", [UserRole.Organizer, UserRole.Admin]),
        new("POST", "/api/v1/events/{id}/reservations", [UserRole.Customer]),
        new("GET", "/api/v1/reservations/{id}", [UserRole.Customer, UserRole.Admin]),
        new("DELETE", "/api/v1/reservations/{id}", [UserRole.Customer]),
        new("GET", "/api/v1/me/reservations", [UserRole.Customer]),
        new("POST", "/api/v1/reservations/{id}/payments", [UserRole.Customer]),
        new("GET", "/api/v1/me/tickets", [UserRole.Customer]),
        new("GET", "/api/v1/tickets/{id}/qr", [UserRole.Customer, UserRole.Admin]),
    ];

    public static readonly IReadOnlyList<(string Method, string Route)> Anonymous =
    [
        ("POST", "/api/v1/auth/register"),
        ("POST", "/api/v1/auth/login"),
        ("POST", "/api/v1/auth/refresh"),
        ("POST", "/api/v1/auth/logout"),
        ("GET", "/api/v1/venues"),
        ("GET", "/api/v1/venues/{id}"),
        ("GET", "/api/v1/halls/{id}"),
        ("GET", "/api/v1/events"),
        ("GET", "/api/v1/events/{id}"),
        ("GET", "/api/v1/events/{id}/seats"),
    ];

    /// <summary>Route with every <c>{parameter}</c> replaced by a fresh id.</summary>
    public static string ToUrl(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, "{[^}]+}", _ => Guid.CreateVersion7().ToString());
}
