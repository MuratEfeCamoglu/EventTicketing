using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.RateLimiting;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventTicketing.IntegrationTests.Features.Platform;

/// <summary>AC-20.1 (bonus). Every test gets its own host, so its limiter windows start empty.</summary>
public sealed class RateLimitingTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-20.1")]
    public async Task Login_EleventhRequestInAMinute_Returns429WithRetryAfter()
    {
        await using var api = LimitedApi();
        using var client = api.CreateClient();

        for (var i = 0; i < RateLimitingSetup.AuthPermitsPerWindow; i++)
        {
            (await LoginAsync(client)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var rejected = await LoginAsync(client);

        await rejected.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    [Trait("AC", "AC-20.1")]
    public async Task Register_SharesTheAuthWindowWithLogin_EleventhRequestReturns429()
    {
        await using var api = LimitedApi();
        using var client = api.CreateClient();

        for (var i = 0; i < RateLimitingSetup.AuthPermitsPerWindow / 2; i++)
        {
            (await LoginAsync(client)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await RegisterAsync(client)).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var rejected = await RegisterAsync(client);

        await rejected.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");
        rejected.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    [Trait("AC", "AC-20.1")]
    public async Task CreateReservation_ThirtyFirstRequestOfAUser_Returns429AndOtherUsersAreNotLimited()
    {
        await using var api = LimitedApi();
        using var client = api.CreateClient();
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var missingEvent = Guid.NewGuid();

        for (var i = 0; i < RateLimitingSetup.ReservationPermitsPerWindow; i++)
        {
            (await client.HoldAsync(missingEvent, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await (await client.HoldAsync(missingEvent, Guid.NewGuid())).ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limit.exceeded");

        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        (await client.HoldAsync(missingEvent, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "AC-20.1")]
    public async Task Login_RateLimitingDisabled_NeverReturns429()
    {
        for (var i = 0; i <= RateLimitingSetup.AuthPermitsPerWindow; i++)
        {
            (await LoginAsync(Client)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private WebApplicationFactory<Program> LimitedApi() =>
        Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Enabled", "true"));

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.com", password = "Wrong-passw0rd" });

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email = $"limit-{Guid.NewGuid():N}@example.com", password = "Passw0rd-123", fullName = "Rate Limit" });
}
