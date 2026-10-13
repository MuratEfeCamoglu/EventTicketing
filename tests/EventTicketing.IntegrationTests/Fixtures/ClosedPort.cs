using System.Net;
using System.Net.Sockets;

namespace EventTicketing.IntegrationTests.Fixtures;

public static class ClosedPort
{
    /// <summary>A local port nothing listens on: bound by the OS, then released (unreachable SMTP/Redis tests).</summary>
    public static int Find()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
