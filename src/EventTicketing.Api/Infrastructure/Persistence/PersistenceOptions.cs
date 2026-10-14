using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Api.Infrastructure.Persistence;

public sealed class ConnectionStringOptions
{
    public const string Section = "ConnectionStrings";

    [Required]
    public string Postgres { get; set; } = string.Empty;

    /// <summary>
    /// Required from v2. Carries <c>abortConnect=false,connectTimeout=1000,syncTimeout=1000</c>, so requests never wait
    /// long on a Redis that is down (ISKELET §5.14).
    /// </summary>
    [Required]
    public string Redis { get; set; } = string.Empty;

    /// <summary>Required from v3: an AMQP URI with the credentials, e.g. <c>amqp://user:password@localhost:5672/</c>.</summary>
    [Required]
    public string RabbitMq { get; set; } = string.Empty;
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public bool MigrateOnStartup { get; set; }
}

public sealed class SeedOptions
{
    public const string Section = "Seed";

    [Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    [Required]
    public string AdminPassword { get; set; } = string.Empty;

    public bool SampleData { get; set; }
}
