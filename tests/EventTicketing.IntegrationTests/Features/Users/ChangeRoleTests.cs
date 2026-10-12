using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EventTicketing.IntegrationTests.Features.Users;

public sealed class ChangeRoleTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task ChangeRole_CustomerToOrganizer_Returns204AndNextTokenCarriesNewRole()
    {
        var (customer, customerTokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(RoleUrl(customer.Id), new { role = "Organizer" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == customer.Id, TestContext.Current.CancellationToken))).Role
            .ShouldBe(UserRole.Organizer);
        new JsonWebToken(customerTokens.AccessToken).GetClaim("role").Value.ShouldBe("Customer");
        var refreshed = await (await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = customerTokens.RefreshToken }, TestContext.Current.CancellationToken)).ReadJsonAsync();
        new JsonWebToken(refreshed.GetProperty("accessToken").GetString()).GetClaim("role").Value.ShouldBe("Organizer");
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task ChangeRole_OrganizerToCustomer_Returns204()
    {
        var organizer = await Factory.CreateUserAsync(UserRole.Organizer);
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(RoleUrl(organizer.Id), new { role = "Customer" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task ChangeRole_ToAdmin_Returns400CannotAssignAdmin()
    {
        var customer = await Factory.CreateUserAsync(UserRole.Customer);
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(RoleUrl(customer.Id), new { role = "Admin" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "user.cannot_assign_admin");
        (await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == customer.Id, TestContext.Current.CancellationToken))).Role
            .ShouldBe(UserRole.Customer);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task ChangeRole_TargetIsAdmin_Returns400CannotAssignAdmin()
    {
        var otherAdmin = await Factory.CreateUserAsync(UserRole.Admin);
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(RoleUrl(otherAdmin.Id), new { role = "Customer" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "user.cannot_assign_admin");
    }

    [Fact]
    public async Task ChangeRole_UnknownUser_Returns404()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(RoleUrl(Guid.CreateVersion7()), new { role = "Organizer" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"role\":\"Superuser\"}")]
    public async Task ChangeRole_MissingOrUnknownRole_Returns400(string json)
    {
        var customer = await Factory.CreateUserAsync(UserRole.Customer);
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsync(RoleUrl(customer.Id), new StringContent(json, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    private static string RoleUrl(Guid userId) => $"/api/v1/admin/users/{userId}/role";

    private async Task AuthenticateAsAdminAsync()
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Admin);
        Client.WithBearerToken(tokens.AccessToken);
    }
}
