using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventTicketing.Api.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    /// <summary>True when the save failed with PostgreSQL <c>23505</c> on <paramref name="constraintName"/>.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName == constraintName;
}
