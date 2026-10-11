using Npgsql;
using Respawn;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>
/// Creates a throw-away database on the local PostgreSQL server for one test run and drops it afterwards (ISKELET §3.2, K-10).
/// The server comes from <c>EVENTTICKETING_TEST_POSTGRES</c>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ServerVariable = "EVENTTICKETING_TEST_POSTGRES";
    private const string DefaultServer = "Host=localhost;Port=5432;Username=postgres;Password=dev-only-postgres";

    private readonly string _serverConnectionString;
    private readonly string _databaseName = $"eventticketing_test_{Guid.NewGuid():N}";
    private Respawner? _respawner;

    public PostgresFixture()
    {
        _serverConnectionString = Environment.GetEnvironmentVariable(ServerVariable) is { Length: > 0 } value ? value : DefaultServer;
        ConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _databaseName,
            Pooling = false,
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public async ValueTask InitializeAsync()
    {
        // The name is generated above from a Guid, so quoting it as an identifier is safe.
        await ExecuteOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");
    }

    /// <summary>Deletes all rows (Respawn). The schema must exist: the API host migrates the database on startup.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });

        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        var builder = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = "postgres", Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
