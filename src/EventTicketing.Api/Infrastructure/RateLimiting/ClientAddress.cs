using System.Net;

namespace EventTicketing.Api.Infrastructure.RateLimiting;

/// <summary>
/// The caller's address for per-IP rate limiting (AC-20.1). Behind the local ngrok tunnel (W-10) every request arrives
/// from the tunnel agent on loopback, so the original client is the first address in <c>X-Forwarded-For</c>; that header
/// is trusted only from loopback, because any remote caller could write it.
/// </summary>
public static class ClientAddress
{
    private const string ForwardedForHeader = "X-Forwarded-For";

    public static string Of(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && IPAddress.IsLoopback(remote))
        {
            var first = context.Request.Headers[ForwardedForHeader].ToString().Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out var client))
            {
                return client.ToString();
            }
        }

        return remote?.ToString() ?? "unknown";
    }
}
