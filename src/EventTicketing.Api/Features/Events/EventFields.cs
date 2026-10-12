using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

public sealed record SectionPriceInput(string Section, decimal Price);

/// <summary>The fields <c>CreateEvent</c> and <c>UpdateEvent</c> share (AC-05.1, AC-05.2).</summary>
public interface IEventFields
{
    Guid? HallId { get; }

    string Title { get; }

    string? Description { get; }

    EventCategory? Category { get; }

    DateTimeOffset? StartsAt { get; }

    DateTimeOffset? EndsAt { get; }

    IReadOnlyList<SectionPriceInput> SectionPrices { get; }
}

public sealed class EventFieldsValidator : AbstractValidator<IEventFields>
{
    public const decimal MaxPrice = 100_000m;

    public EventFieldsValidator()
    {
        RuleFor(r => r.HallId).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(4000);
        RuleFor(r => r.Category).NotNull().IsInEnum();
        RuleFor(r => r.StartsAt).NotNull();
        RuleFor(r => r.EndsAt).NotNull().GreaterThan(r => r.StartsAt).When(r => r.StartsAt is not null);
        RuleFor(r => r.SectionPrices).NotNull();
        RuleForEach(r => r.SectionPrices).ChildRules(price =>
        {
            price.RuleFor(p => p.Section).NotEmpty().MaximumLength(50);
            price.RuleFor(p => p.Price).GreaterThan(0).LessThanOrEqualTo(MaxPrice).PrecisionScale(8, 2, ignoreTrailingZeros: true);
        });
        RuleFor(r => r.SectionPrices)
            .Must(prices => prices.Where(p => p?.Section is not null).Select(p => p.Section.Trim()).GroupBy(s => s, StringComparer.Ordinal).All(g => g.Count() == 1))
            .When(r => r.SectionPrices is not null)
            .WithMessage("Each section may be priced only once.");
    }
}

public static class EventFieldsExtensions
{
    /// <summary>Trimmed section names with their prices.</summary>
    public static IReadOnlyList<(string Section, decimal Price)> NormalizedPrices(this IEventFields fields) =>
        fields.SectionPrices.Select(p => (p.Section.Trim(), p.Price)).ToList();

    /// <summary>The hall must exist and every priced section must be one of its sections; both are <c>400 validation.failed</c>.</summary>
    public static async Task<Error?> ValidateHallAsync(this AppDbContext db, IEventFields fields, CancellationToken cancellationToken)
    {
        var hallId = fields.HallId!.Value;
        if (!await db.Halls.AnyAsync(h => h.Id == hallId, cancellationToken))
        {
            return DomainErrors.Validation("hallId", "The hall does not exist.");
        }

        var sections = await db.Seats.Where(s => s.HallId == hallId).Select(s => s.Section).Distinct().ToListAsync(cancellationToken);
        var unknown = fields.NormalizedPrices().Select(p => p.Section).Except(sections, StringComparer.Ordinal).ToList();
        return unknown.Count == 0
            ? null
            : DomainErrors.Validation("sectionPrices", $"The hall has no section(s): {string.Join(", ", unknown)}.");
    }
}
