using System.Text.Json;

namespace EventTicketing.IntegrationTests.Fixtures;

public sealed record MailpitAttachment(string FileName, string ContentType);

public sealed record MailpitMessage(string Id, string Subject, IReadOnlyList<MailpitAttachment> Attachments);

/// <summary>
/// Reads the local Mailpit through its HTTP API (AC-12.1). The base URL comes from <c>EVENTTICKETING_TEST_MAILPIT</c>,
/// default <c>http://localhost:8025</c>; the API sends SMTP to the <c>Smtp:*</c> defaults (localhost:1025).
/// </summary>
public static class Mailpit
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("EVENTTICKETING_TEST_MAILPIT") ?? "http://localhost:8025"),
    };

    /// <summary>All messages addressed to <paramref name="recipient"/>, with their attachments.</summary>
    public static async Task<IReadOnlyList<MailpitMessage>> MessagesToAsync(string recipient, CancellationToken cancellationToken)
    {
        var search = await GetJsonAsync($"/api/v1/search?query={Uri.EscapeDataString($"to:\"{recipient}\"")}", cancellationToken);
        var messages = new List<MailpitMessage>();
        foreach (var summary in search.GetProperty("messages").EnumerateArray())
        {
            var id = summary.GetProperty("ID").GetString()!;
            var detail = await GetJsonAsync($"/api/v1/message/{id}", cancellationToken);
            var attachments = detail.GetProperty("Attachments").EnumerateArray()
                .Select(a => new MailpitAttachment(a.GetProperty("FileName").GetString()!, a.GetProperty("ContentType").GetString()!))
                .ToList();
            messages.Add(new MailpitMessage(id, detail.GetProperty("Subject").GetString()!, attachments));
        }

        return messages;
    }

    private static async Task<JsonElement> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.Clone();
    }
}
