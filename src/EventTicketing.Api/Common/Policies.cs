using EventTicketing.Api.Domain;
using Microsoft.AspNetCore.Authorization;

namespace EventTicketing.Api.Common;

/// <summary>Authorization policies (ISKELET §5.11). Resource ownership is checked in handlers.</summary>
public static class Policies
{
    public const string Admin = nameof(Admin);
    public const string OrganizerOrAdmin = nameof(OrganizerOrAdmin);
    public const string Customer = nameof(Customer);
    public const string CustomerOrAdmin = nameof(CustomerOrAdmin);
    public const string Authenticated = nameof(Authenticated);

    public static AuthorizationBuilder AddEventTicketingPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(Admin, p => p.RequireRole(nameof(UserRole.Admin)))
        .AddPolicy(OrganizerOrAdmin, p => p.RequireRole(nameof(UserRole.Organizer), nameof(UserRole.Admin)))
        .AddPolicy(Customer, p => p.RequireRole(nameof(UserRole.Customer)))
        .AddPolicy(CustomerOrAdmin, p => p.RequireRole(nameof(UserRole.Customer), nameof(UserRole.Admin)))
        .AddPolicy(Authenticated, p => p.RequireAuthenticatedUser());
}
