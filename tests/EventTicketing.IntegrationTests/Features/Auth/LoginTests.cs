using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EventTicketing.IntegrationTests.Features.Auth;

public sealed class LoginTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Url = "/api/v1/auth/login";

    [Fact]
    [Trait("AC", "AC-01.4")]
    public async Task Login_ValidCredentials_Returns200WithFifteenMinuteAccessTokenAndRefreshToken()
    {
        var user = await Factory.CreateUserAsync(UserRole.Customer);

        var response = await Client.PostAsJsonAsync(Url, new { email = user.Email.ToUpperInvariant(), password = user.Password }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        var jwt = new JsonWebToken(body.GetProperty("accessToken").GetString());
        jwt.GetPayloadValue<long>("exp").ShouldBe(jwt.GetPayloadValue<long>("iat") + 900);
        jwt.Subject.ShouldBe(user.Id.ToString());
        jwt.GetClaim("role").Value.ShouldBe("Customer");
        jwt.GetClaim("email").Value.ShouldBe(user.Email);
        jwt.Id.ShouldNotBeNullOrEmpty();
        body.GetProperty("accessTokenExpiresAt").GetDateTimeOffset().ShouldBe(Factory.Clock.GetUtcNow().AddMinutes(15), TimeSpan.FromSeconds(1));
        body.GetProperty("refreshToken").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("AC", "AC-01.4")]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnTheSame401()
    {
        var user = await Factory.CreateUserAsync(UserRole.Customer);

        var wrongPassword = await Client.PostAsJsonAsync(Url, new { email = user.Email, password = "Wrong-pass1" }, TestContext.Current.CancellationToken);
        var unknownEmail = await Client.PostAsJsonAsync(Url, new { email = "nobody@example.com", password = "Wrong-pass1" }, TestContext.Current.CancellationToken);

        var first = await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        var second = await unknownEmail.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        WithoutTraceId(first).ShouldBe(WithoutTraceId(second));
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        var response = await Client.PostAsJsonAsync(Url, new { email = "", password = "" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task AccessToken_ValidatedAgainstTimeProvider_AcceptedWithinLifetimeRejectedAfter()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Admin);
        Client.WithBearerToken(tokens.AccessToken);
        var url = $"/api/v1/admin/users/{Guid.CreateVersion7()}/role";

        Factory.Clock.Advance(TimeSpan.FromMinutes(14));
        var withinLifetime = await Client.PutAsJsonAsync(url, new { role = "Organizer" }, TestContext.Current.CancellationToken);

        // 15 minutes + 30 s clock skew.
        Factory.Clock.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(31)));
        var expired = await Client.PutAsJsonAsync(url, new { role = "Organizer" }, TestContext.Current.CancellationToken);

        await withinLifetime.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
        await expired.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    private static string WithoutTraceId(System.Text.Json.JsonElement problem) =>
        string.Join(";", problem.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
}
