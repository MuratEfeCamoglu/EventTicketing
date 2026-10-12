using System.Net;
using System.Net.Http.Json;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Platform;

public sealed class LogPrivacyTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-18.4")]
    public async Task AuthFlow_Logs_ContainNoPasswordsTokensOrFullEmail()
    {
        const string email = "privacy.check@example.com";
        const string password = "S3cret-Passw0rd";
        var ct = TestContext.Current.CancellationToken;

        (await Client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, fullName = "Privacy Check" }, ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong-passw0rd" }, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var tokens = await Client.LoginAsync(email, password);
        var refreshed = await (await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, ct)).ReadJsonAsync();
        var refreshedAccessToken = refreshed.GetProperty("accessToken").GetString()!;
        var refreshedRefreshToken = refreshed.GetProperty("refreshToken").GetString()!;
        (await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refreshedRefreshToken }, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var logText = string.Join(
            Environment.NewLine,
            Factory.Logs.Events.Select(e => $"{e.RenderMessage()} {string.Join(' ', e.Properties.Values)} {e.Exception}"));

        logText.ShouldNotBeEmpty();
        foreach (var secret in new[] { password, "Wrong-passw0rd", tokens.AccessToken, tokens.RefreshToken, refreshedAccessToken, refreshedRefreshToken, email })
        {
            logText.ShouldNotContain(secret, Case.Insensitive);
        }

        logText.ShouldContain("p***@example.com");
    }

    [Fact]
    [Trait("AC", "AC-18.4")]
    public async Task PaymentFlow_Logs_ContainNoPaymentTokensOrBearerToken()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, Api.Domain.UserRole.Customer);
        Client.WithBearerToken(tokens.AccessToken);
        var paid = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        (await Client.PayAsync(paid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var declined = await Client.HoldOrFailAsync(eventId, seatIds[1]);
        (await Client.PayAsync(declined, Api.Infrastructure.Payments.MockPaymentGateway.DeclinedToken)).StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);

        var logText = string.Join(
            Environment.NewLine,
            Factory.Logs.Events.Select(e => $"{e.RenderMessage()} {string.Join(' ', e.Properties.Values)} {e.Exception}"));

        logText.ShouldContain(paid.ToString());
        foreach (var secret in new[] { "tok_success", "tok_declined", tokens.AccessToken, tokens.RefreshToken })
        {
            logText.ShouldNotContain(secret, Case.Insensitive);
        }
    }
}
