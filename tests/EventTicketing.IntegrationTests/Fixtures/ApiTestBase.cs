using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>Base for integration test classes: every test starts from an empty database and an empty log sink.</summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    public async ValueTask InitializeAsync() => await Factory.ResetAsync();

    /// <summary>Runs <paramref name="action"/> against the test database with a fresh <see cref="AppDbContext"/>.</summary>
    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
