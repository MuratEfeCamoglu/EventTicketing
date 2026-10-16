using System.Net;
using EventTicketing.Api.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace EventTicketing.UnitTests.Infrastructure;

public sealed class ClientAddressTests
{
    private static HttpContext Request(string remote, string? forwardedFor = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (forwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        return context;
    }

    [Fact]
    [Trait("AC", "AC-20.1")]
    public void Of_RequestThroughLocalTunnel_UsesTheOriginalClientFromForwardedFor()
    {
        ClientAddress.Of(Request("127.0.0.1", "203.0.113.5, 76.76.21.9")).ShouldBe("203.0.113.5");
        ClientAddress.Of(Request("::1", "198.51.100.7")).ShouldBe("198.51.100.7");
    }

    [Fact]
    [Trait("AC", "AC-20.1")]
    public void Of_DirectRequest_IgnoresForwardedForAndUsesTheConnection()
    {
        // A remote caller could write any X-Forwarded-For; only the local tunnel agent is trusted to set it.
        ClientAddress.Of(Request("203.0.113.5", "10.0.0.1")).ShouldBe("203.0.113.5");
    }

    [Fact]
    [Trait("AC", "AC-20.1")]
    public void Of_LocalRequestWithoutForwardedFor_UsesTheConnection()
    {
        ClientAddress.Of(Request("127.0.0.1")).ShouldBe("127.0.0.1");
        ClientAddress.Of(Request("127.0.0.1", "not-an-ip")).ShouldBe("127.0.0.1");
    }
}
