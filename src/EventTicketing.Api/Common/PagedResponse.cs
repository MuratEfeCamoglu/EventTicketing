using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Common;

/// <summary>Paged list response (ISKELET §5.12).</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public static class Paging
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Counts <paramref name="query"/> and reads one page of it; the query must already be ordered.</summary>
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var currentPage = page ?? DefaultPage;
        var size = pageSize ?? DefaultPageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((currentPage - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, currentPage, size, totalCount, (int)Math.Ceiling(totalCount / (double)size));
    }
}
