using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Notifications;

public sealed class ConnectionStringOptions
{
    public const string Section = "ConnectionStrings";

    /// <summary>Same PostgreSQL server as the API; this service keeps its tables in the <c>notifications</c> schema.</summary>
    [Required]
    public string Postgres { get; set; } = string.Empty;

    /// <summary>An AMQP URI with the credentials, e.g. <c>amqp://user:password@localhost:5672/</c>.</summary>
    [Required]
    public string RabbitMq { get; set; } = string.Empty;
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public bool MigrateOnStartup { get; set; }
}
