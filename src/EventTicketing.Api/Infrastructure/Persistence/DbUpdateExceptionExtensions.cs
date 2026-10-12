using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventTicketing.Api.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    /// <summary>True when the save failed with PostgreSQL <c>23505</c>, on <paramref name="constraintName"/> when one is given.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string? constraintName = null) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && (constraintName is null || postgres.ConstraintName == constraintName);
}
