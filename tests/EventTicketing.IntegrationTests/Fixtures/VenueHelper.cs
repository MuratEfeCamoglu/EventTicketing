using System.Net.Http.Json;
using EventTicketing.Api.Domain;

namespace EventTicketing.IntegrationTests.Fixtures;

public static class VenueHelper
{
    /// <summary>Creates a user with <paramref name="role"/>, logs in and sets the bearer token on <paramref name="client"/>.</summary>
    public static async Task<TestUser> AuthenticateAsAsync(this HttpClient client, ApiFactory factory, UserRole role)
    {
        var (user, tokens) = await factory.CreateUserAndLoginAsync(client, role);
        client.WithBearerToken(tokens.AccessToken);
        return user;
    }

    /// <summary>Creates a venue through the API; the client must be authenticated as Admin.</summary>
    public static async Task<Guid> CreateVenueAsync(this HttpClient client, string name, string city)
    {
        var response = await client.PostAsJsonAsync("/api/v1/venues", new { name, city, address = $"{name} address" });
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    /// <summary>Creates a hall through the API; the client must be authenticated as Admin.</summary>
    public static async Task<Guid> CreateHallAsync(this HttpClient client, Guid venueId, string name, params object[] sections)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/venues/{venueId}/halls", new { name, sections });
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    /// <summary>A hall section in the <c>POST /venues/{id}/halls</c> request shape.</summary>
    public static object Section(string name, params (string Label, int SeatCount)[] rows) =>
        new { name, rows = rows.Select(r => new { label = r.Label, seatCount = r.SeatCount }).ToArray() };
}
