using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Api.Infrastructure.Persistence;

public sealed class ConnectionStringOptions
{
    public const string Section = "ConnectionStrings";

    [Required]
    public string Postgres { get; set; } = string.Empty;
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
