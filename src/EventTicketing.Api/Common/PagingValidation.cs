using FluentValidation;

namespace EventTicketing.Api.Common;

public static class PagingValidation
{
    /// <summary><c>page</c> ≥ 1 and 1 ≤ <c>pageSize</c> ≤ 100 when given (AC-06.2).</summary>
    public static void AddPagingRules<T>(this AbstractValidator<T> validator, Func<T, int?> page, Func<T, int?> pageSize)
    {
        validator.RuleFor(r => page(r)).GreaterThanOrEqualTo(1).OverridePropertyName("page");
        validator.RuleFor(r => pageSize(r)).InclusiveBetween(1, Paging.MaxPageSize).OverridePropertyName("pageSize");
    }
}
