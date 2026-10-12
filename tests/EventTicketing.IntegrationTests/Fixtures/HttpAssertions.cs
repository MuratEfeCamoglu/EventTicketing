using System.Net;
using System.Text.Json;

namespace EventTicketing.IntegrationTests.Fixtures;

public static class HttpAssertions
{
    /// <summary>Asserts an ISKELET §5.12 ProblemDetails response and returns its body.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.ReadJsonAsync();
        body.GetProperty("code").GetString().ShouldBe(code);
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        return body;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
