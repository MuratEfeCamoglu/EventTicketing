using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Features.Auth;

public sealed partial class AuthorizationMatrixTests(ApiFactory factory) : ApiTestBase(factory)
{
    public static TheoryData<string, string> ProtectedEndpoints()
    {
        var data = new TheoryData<string, string>();
        foreach (var endpoint in EndpointAuthorizationMatrix.Protected)
        {
            data.Add(endpoint.Method, endpoint.Route);
        }

        return data;
    }

    public static TheoryData<string, string, UserRole> ForbiddenRoles()
    {
        var data = new TheoryData<string, string, UserRole>();
        foreach (var endpoint in EndpointAuthorizationMatrix.Protected)
        {
            foreach (var role in Enum.GetValues<UserRole>().Except(endpoint.AllowedRoles))
            {
                data.Add(endpoint.Method, endpoint.Route, role);
            }
        }

        return data;
    }

    [Theory]
    [Trait("AC", "AC-03.1")]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_NoToken_Returns401(string method, string route)
    {
        var response = await Client.SendAsync(CreateRequest(method, route), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Theory]
    [Trait("AC", "AC-03.1")]
    [MemberData(nameof(ForbiddenRoles))]
    public async Task ProtectedEndpoint_RoleNotAllowed_Returns403(string method, string route, UserRole role)
    {
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, role);
        Client.WithBearerToken(tokens.AccessToken);

        var response = await Client.SendAsync(CreateRequest(method, route), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Fact]
    [Trait("AC", "AC-03.1")]
    public void MappedEndpoints_All_AppearInMatrix()
    {
        var mapped = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => $"{m} {DocumentedRoute(e.RoutePattern.RawText!)}"))
            .Order()
            .ToList();

        var documented = EndpointAuthorizationMatrix.Protected.Select(e => $"{e.Method} {e.Route}")
            .Concat(EndpointAuthorizationMatrix.Anonymous.Select(e => $"{e.Method} {e.Route}"))
            .Order()
            .ToList();

        mapped.ShouldBe(documented);
    }

    private static HttpRequestMessage CreateRequest(string method, string route) =>
        new(new HttpMethod(method), EndpointAuthorizationMatrix.ToUrl(route))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

    /// <summary>The matrix form of a mapped route: version 1 in the URL, no constraints (<c>/api/v{version:apiVersion}/x/{id:guid}</c> → <c>/api/v1/x/{id}</c>).</summary>
    private static string DocumentedRoute(string pattern) =>
        RouteConstraint().Replace(pattern.Replace(EndpointRegistration.VersionedPrefix, EndpointRegistration.ApiPrefix, StringComparison.Ordinal), "}");

    /// <summary>Strips route constraints: <c>{id:guid}</c> → <c>{id}</c>.</summary>
    [GeneratedRegex(":[^}]+}")]
    private static partial Regex RouteConstraint();
}
