using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Auth;

public sealed class RegisterTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Url = "/api/v1/auth/register";

    [Fact]
    [Trait("AC", "AC-01.1")]
    public async Task Register_ValidRequest_Returns201CustomerWithHashedPassword()
    {
        var response = await Client.PostAsJsonAsync(Url, new { email = "Ayse@Example.com", password = "Passw0rd!", fullName = "Ayşe Yılmaz" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        body.GetProperty("email").GetString().ShouldBe("ayse@example.com");
        body.GetProperty("fullName").GetString().ShouldBe("Ayşe Yılmaz");
        body.GetProperty("role").GetString().ShouldBe("Customer");

        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken));
        user.Role.ShouldBe(UserRole.Customer);
        user.PasswordHash.ShouldNotBe("Passw0rd!");
        new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, "Passw0rd!").ShouldNotBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public async Task Register_SameEmailDifferentCase_Returns409EmailTaken()
    {
        (await Client.PostAsJsonAsync(Url, new { email = "mehmet@example.com", password = "Passw0rd!", fullName = "Mehmet" }, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Client.PostAsJsonAsync(Url, new { email = "MEHMET@Example.com", password = "Passw0rd!", fullName = "Mehmet" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "auth.email_taken");
    }

    [Theory]
    [Trait("AC", "AC-01.3")]
    [InlineData("Pass0rd")]
    [InlineData("onlyletters")]
    [InlineData("1234567890")]
    public async Task Register_WeakPassword_Returns400WithPasswordError(string password)
    {
        var response = await Client.PostAsJsonAsync(Url, new { email = "weak@example.com", password, fullName = "Weak" }, TestContext.Current.CancellationToken);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").GetProperty("password").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [Trait("AC", "AC-01.3")]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Register_InvalidEmail_Returns400WithEmailError(string email)
    {
        var response = await Client.PostAsJsonAsync(Url, new { email, password = "Passw0rd!", fullName = "Someone" }, TestContext.Current.CancellationToken);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").GetProperty("email").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [Trait("AC", "AC-01.1")]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Register_FullNameOutOfRange_Returns400WithFullNameError(int length)
    {
        var response = await Client.PostAsJsonAsync(Url, new { email = "name@example.com", password = "Passw0rd!", fullName = new string('a', length) }, TestContext.Current.CancellationToken);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").GetProperty("fullName").GetArrayLength().ShouldBeGreaterThan(0);
    }
}
