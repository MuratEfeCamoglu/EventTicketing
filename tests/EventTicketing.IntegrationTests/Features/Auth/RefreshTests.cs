using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Auth;

public sealed class RefreshTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Url = "/api/v1/auth/refresh";

    [Fact]
    [Trait("AC", "AC-02.1")]
    public async Task Refresh_ValidToken_Returns200WithNewTokensAndRevokesOldOne()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);

        var response = await RefreshAsync(tokens.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        var newRefreshToken = body.GetProperty("refreshToken").GetString();
        newRefreshToken.ShouldNotBe(tokens.RefreshToken);
        body.GetProperty("accessToken").GetString().ShouldNotBeNullOrEmpty();
        (await RefreshAsync(newRefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RefreshAsync(tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("AC", "AC-02.2")]
    public async Task Refresh_ReusedToken_Returns401ReusedAndRevokesWholeFamily()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);
        var rotated = (await (await RefreshAsync(tokens.RefreshToken)).ReadJsonAsync()).GetProperty("refreshToken").GetString()!;

        var reuse = await RefreshAsync(tokens.RefreshToken);

        await reuse.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.refresh_token_reused");
        await (await RefreshAsync(rotated)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.refresh_token_reused");
        (await WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.RevokedAt == null, TestContext.Current.CancellationToken))).ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-02.2")]
    public async Task Refresh_SameTokenConcurrently_ExactlyOneSucceeds()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);
        using var barrier = new Barrier(2);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            using var client = Factory.CreateClient();
            barrier.SignalAndWait(TestContext.Current.CancellationToken);
            return await client.PostAsJsonAsync(Url, new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);
        })));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-02.3")]
    public async Task Refresh_TokenOlderThanSevenDays_Returns401Expired()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);

        Factory.Clock.Advance(TimeSpan.FromDays(7).Add(TimeSpan.FromSeconds(1)));
        var response = await RefreshAsync(tokens.RefreshToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.refresh_token_expired");
    }

    [Fact]
    [Trait("AC", "AC-02.3")]
    public async Task Refresh_UnknownToken_Returns401Invalid()
    {
        var response = await RefreshAsync("this-token-was-never-issued");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.refresh_token_invalid");
    }

    [Fact]
    [Trait("AC", "AC-02.5")]
    public async Task Login_RefreshToken_IsStoredOnlyAsSha256Hex()
    {
        var (user, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);

        var stored = await WithDbAsync(db => db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == user.Id, TestContext.Current.CancellationToken));

        stored.TokenHash.ShouldNotBe(tokens.RefreshToken);
        stored.TokenHash.ShouldMatch("^[0-9a-f]{64}$");
        stored.TokenHash.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokens.RefreshToken))));
    }

    [Fact]
    public async Task Refresh_EmptyToken_Returns400()
    {
        var response = await RefreshAsync("");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        Client.PostAsJsonAsync(Url, new { refreshToken }, TestContext.Current.CancellationToken);
}
