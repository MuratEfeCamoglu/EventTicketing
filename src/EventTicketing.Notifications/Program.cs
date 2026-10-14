namespace EventTicketing.Notifications;

// An explicit, namespaced entry point: a top-level Program would be a public global type and clash with the API's
// Program in the integration tests, which reference both projects.
internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = NotificationsHost.CreateBuilder(new HostApplicationBuilderSettings { Args = args });
        using var host = builder.Build();

        await NotificationsHost.InitializeDatabaseAsync(host.Services);
        await host.RunAsync();
    }
}
