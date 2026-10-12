using EventTicketing.Api.Features.Venues;

namespace EventTicketing.UnitTests.Features;

public sealed class CreateHallValidatorTests
{
    private readonly CreateHall.Validator _validator = new();

    [Theory]
    [Trait("AC", "AC-04.3")]
    [InlineData(1, true)]
    [InlineData(5000, true)]
    [InlineData(5001, false)]
    public void Validate_TotalSeatCount_MustBeOneToFiveThousand(int seats, bool valid)
    {
        var request = new CreateHall.Request("Hall", [new("Floor", [new("A", seats)])]);

        _validator.Validate(request).IsValid.ShouldBe(valid);
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    public void Validate_DuplicateSectionName_IsInvalid()
    {
        var request = new CreateHall.Request("Hall", [new("Floor", [new("A", 1)]), new(" Floor ", [new("B", 1)])]);

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    public void Validate_DuplicateRowLabelInSection_IsInvalid()
    {
        var request = new CreateHall.Request("Hall", [new("Floor", [new("A", 1), new("A", 2)])]);

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    public void Validate_SameRowLabelInDifferentSections_IsValid()
    {
        var request = new CreateHall.Request("Hall", [new("Floor", [new("A", 1)]), new("Balcony", [new("A", 1)])]);

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }
}
