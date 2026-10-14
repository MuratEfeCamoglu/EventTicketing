using System.Net;
using System.Text.Json;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Platform;

/// <summary>AC-20.2 (bonus): URL segment versioning with Asp.Versioning.Http.</summary>
public sealed class ApiVersioningTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-20.2")]
    public async Task Get_Version1_ReportsSupportedVersions()
    {
        var response = await Client.GetAsync("/api/v1/events", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("api-supported-versions").ShouldBe(["1.0"]);
    }

    [Fact]
    [Trait("AC", "AC-20.2")]
    public async Task Get_UnknownVersion_Returns400ProblemDetails()
    {
        var response = await Client.GetAsync("/api/v2/events", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("AC", "AC-20.2")]
    public async Task Get_UnknownPathOfVersion1_StillReturns404()
    {
        var response = await Client.GetAsync("/api/v1/no-such-resource", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-20.2")]
    public async Task OpenApiDocument_ListsConcreteVersion1Paths()
    {
        var response = await Client.GetAsync("/openapi/v1.json", Ct);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.ShouldContain("/api/v1/events");
        paths.ShouldNotContain(path => path.Contains("{version}", StringComparison.Ordinal));
    }
}
