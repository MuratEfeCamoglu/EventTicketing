using System.Text.Json;
using EventTicketing.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventTicketing.UnitTests.Common;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Returns500ServerErrorWithoutExceptionDetails()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("secret internal detail"));

        status.ShouldBe(StatusCodes.Status500InternalServerError);
        body.GetProperty("code").GetString().ShouldBe("server.error");
        body.TryGetProperty("traceId", out _).ShouldBeTrue();
        body.GetRawText().ShouldNotContain("secret internal detail");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyException_Returns409ConcurrencyConflict()
    {
        var (status, body) = await HandleAsync(new DbUpdateConcurrencyException("conflict"));

        status.ShouldBe(StatusCodes.Status409Conflict);
        body.GetProperty("code").GetString().ShouldBe("concurrency.conflict");
    }

    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails(o => o.CustomizeProblemDetails = ProblemDetailsSetup.Customize)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Headers.Accept = "application/json";
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return (httpContext.Response.StatusCode, document.RootElement.Clone());
    }
}
