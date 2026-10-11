using System.Net;
using System.Text.Json;
using EventTicketing.IntegrationTests.Fixtures;
using Serilog.Events;

namespace EventTicketing.IntegrationTests.Features.Platform;

public sealed class RequestLoggingTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-18.1")]
    public async Task UnknownRoute_ProblemDetailsTraceId_EqualsRequestLogTraceId()
    {
        const string path = "/api/v1/does-not-exist";

        var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("code").GetString().ShouldBe("resource.not_found");
        var traceId = body.RootElement.GetProperty("traceId").GetString();
        traceId.ShouldNotBeNullOrEmpty();

        var requestLog = SingleRequestLog(path);
        Scalar(requestLog, "StatusCode").ShouldBe(404);
        Scalar(requestLog, "TraceId").ShouldBe(traceId);
    }

    [Fact]
    [Trait("AC", "AC-18.1")]
    public async Task SuccessfulRequest_WritesRequestLogWithPathStatusElapsedAndTraceId()
    {
        const string path = "/openapi/v1.json";

        var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var requestLog = SingleRequestLog(path);
        Scalar(requestLog, "StatusCode").ShouldBe(200);
        Scalar(requestLog, "Elapsed").ShouldBeOfType<double>();
        Scalar(requestLog, "TraceId").ShouldBeOfType<string>().Length.ShouldBe(32);
    }

    private LogEvent SingleRequestLog(string path) =>
        Factory.Logs.Events
            .Where(e => e.Properties.TryGetValue("RequestPath", out var value) && value is ScalarValue { Value: string logged } && logged == path)
            .ShouldHaveSingleItem();

    private static object? Scalar(LogEvent logEvent, string property)
    {
        logEvent.Properties.ShouldContainKey(property);
        return logEvent.Properties[property].ShouldBeOfType<ScalarValue>().Value;
    }
}
