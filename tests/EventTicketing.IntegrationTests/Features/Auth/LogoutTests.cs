using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Auth;

public sealed class LogoutTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-02.4")]
    public async Task Logout_ValidToken_Returns204AndTokenCannotRefresh()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);

        var logout = await Client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);
        var refresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("AC", "AC-02.4")]
    public async Task Logout_UnknownToken_Returns204()
    {
        var logout = await Client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "never-issued" }, TestContext.Current.CancellationToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_EmptyToken_Returns400()
    {
        var logout = await Client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "" }, TestContext.Current.CancellationToken);

        await logout.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }
}
