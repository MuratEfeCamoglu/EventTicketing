using System.Net;
using System.Text.Json;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Platform;

public sealed class OpenApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task GetOpenApiDocument_Always_Returns200WithOpenApiJson()
    {
        var response = await Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("openapi").GetString().ShouldNotBeNullOrEmpty();
    }
}
