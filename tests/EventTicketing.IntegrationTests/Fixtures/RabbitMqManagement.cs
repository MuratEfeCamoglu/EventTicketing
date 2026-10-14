using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EventTicketing.IntegrationTests.Fixtures;

public sealed record RabbitMqMessage(string Payload, JsonElement Headers);

/// <summary>
/// The RabbitMQ management HTTP API on port 15672 of <see cref="ApiFactory.RabbitMqServer"/>, with the same credentials:
/// creates and deletes the test run's virtual host and purges and reads its queues (AC-17.4).
/// </summary>
public static class RabbitMqManagement
{
    private const int ManagementPort = 15672;
    private static readonly HttpClient Http = CreateClient();

    public static async Task CreateVirtualHostAsync(string virtualHost)
    {
        (await Http.PutAsJsonAsync($"/api/vhosts/{Uri.EscapeDataString(virtualHost)}", new { })).EnsureSuccessStatusCode();
        (await Http.PutAsJsonAsync(
                $"/api/permissions/{Uri.EscapeDataString(virtualHost)}/{Uri.EscapeDataString(UserName)}",
                new { configure = ".*", write = ".*", read = ".*" }))
            .EnsureSuccessStatusCode();
    }

    /// <summary>Deletes the virtual host with its exchanges and queues; one that is already gone is fine.</summary>
    public static async Task DeleteVirtualHostAsync(string virtualHost)
    {
        using var response = await Http.DeleteAsync($"/api/vhosts/{Uri.EscapeDataString(virtualHost)}");
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>Deletes every message in <paramref name="queue"/>; a queue that does not exist yet is already empty.</summary>
    public static async Task PurgeAsync(string queue, CancellationToken cancellationToken)
    {
        using var response = await Http.DeleteAsync($"{QueuePath(queue)}/contents", cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>Takes (acknowledges) up to <paramref name="count"/> messages from <paramref name="queue"/>; empty if there are none.</summary>
    public static async Task<IReadOnlyList<RabbitMqMessage>> TakeAsync(string queue, int count, CancellationToken cancellationToken)
    {
        using var response = await Http.PostAsJsonAsync(
            $"{QueuePath(queue)}/get",
            new { count, ackmode = "ack_requeue_false", encoding = "auto" },
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.EnumerateArray()
            .Select(m => new RabbitMqMessage(m.GetProperty("payload").GetString()!, m.GetProperty("properties").GetProperty("headers").Clone()))
            .ToList();
    }

    private static string UserName => Uri.UnescapeDataString(ApiFactory.RabbitMqServer.UserInfo.Split(':')[0]);

    /// <summary>A queue of this test run's virtual host.</summary>
    private static string QueuePath(string queue) =>
        $"/api/queues/{Uri.EscapeDataString(ApiFactory.RabbitMqVirtualHost)}/{Uri.EscapeDataString(queue)}";

    private static HttpClient CreateClient()
    {
        var server = ApiFactory.RabbitMqServer;
        var client = new HttpClient { BaseAddress = new UriBuilder("http", server.Host, ManagementPort).Uri };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(server.UserInfo))));
        return client;
    }
}
