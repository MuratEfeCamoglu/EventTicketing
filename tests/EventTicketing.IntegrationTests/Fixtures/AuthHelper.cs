using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Fixtures;

public sealed record TestUser(Guid Id, string Email, string Password, UserRole Role);

public sealed record LoginTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

public static class AuthHelper
{
    public const string DefaultPassword = "Passw0rd-123";

    /// <summary>Inserts a user directly (any role, including Admin, which the API cannot grant).</summary>
    public static async Task<TestUser> CreateUserAsync(this ApiFactory factory, UserRole role, string? email = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = new User(email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.com", $"Test {role}", role, factory.Clock.GetUtcNow());
        user.SetPasswordHash(hasher.HashPassword(user, DefaultPassword));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new TestUser(user.Id, user.Email, DefaultPassword, role);
    }

    public static async Task<LoginTokens> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginTokens>())!;
    }

    /// <summary>Creates a user with <paramref name="role"/> and returns an access token for it.</summary>
    public static async Task<(TestUser User, LoginTokens Tokens)> CreateUserAndLoginAsync(this ApiFactory factory, HttpClient client, UserRole role)
    {
        var user = await factory.CreateUserAsync(role);
        return (user, await client.LoginAsync(user.Email, user.Password));
    }

    public static HttpClient WithBearerToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
