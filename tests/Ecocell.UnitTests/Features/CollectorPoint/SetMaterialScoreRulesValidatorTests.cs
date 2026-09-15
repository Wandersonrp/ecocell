using Ecocell.Api.Enums;
using Ecocell.Api.Features.CollectorPoint;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class SetMaterialScoreRulesValidatorTests
{
    private readonly SetMaterialScoreRules.Validator _validator = new();

    public static TheoryData<decimal> InvalidPoints => new()
    {
        0m,
        -1m,
        100_000_000m,
        1.001m,
    };

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var result = await _validator.ValidateAsync(ValidCommand());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenCollectorPointIdIsEmpty()
    {
        var command = ValidCommand() with { CollectorPointId = Guid.Empty };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.PropertyName == nameof(SetMaterialScoreRules.Command.CollectorPointId));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRulesIsNull()
    {
        var command = ValidCommand() with { Rules = null };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Informe de 1 a 7 regras de pontuação.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRulesIsEmpty()
    {
        var command = ValidCommand() with { Rules = [] };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Informe de 1 a 7 regras de pontuação.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRulesHasMoreThanSevenItems()
    {
        var rules = Enumerable.Range(0, 8)
            .Select(index => new SetMaterialScoreRules.RuleInput(
                (ElectronicMaterial)((index % 7) + 1),
                index + 1,
                MaterialScoreUnit.PerUnit))
            .ToList();
        var command = new SetMaterialScoreRules.Command(Guid.NewGuid(), rules);

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Informe de 1 a 7 regras de pontuação.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
    {
        var duplicate = new SetMaterialScoreRules.RuleInput(
            ElectronicMaterial.Battery,
            20m,
            MaterialScoreUnit.PerKilogram);
        var command = ValidCommand() with { Rules = [ValidRule(), duplicate] };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Cada material pode aparecer somente uma vez.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
    {
        var command = ValidCommand() with
        {
            Rules = [ValidRule() with { Material = (ElectronicMaterial)999 }],
        };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "O material informado é inválido.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUnitIsUndefined()
    {
        var command = ValidCommand() with
        {
            Rules = [ValidRule() with { Unit = (MaterialScoreUnit)999 }],
        };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "A unidade de pontuação informada é inválida.");
    }

    [Theory]
    [MemberData(nameof(InvalidPoints))]
    public async Task Validate_ShouldFail_WhenPointsDoesNotFitNumericTenTwo(decimal points)
    {
        var command = ValidCommand() with { Rules = [ValidRule() with { Points = points }] };

        var result = await _validator.ValidateAsync(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName.EndsWith(nameof(SetMaterialScoreRules.RuleInput.Points)));
    }

    private static SetMaterialScoreRules.Command ValidCommand() =>
        new(Guid.NewGuid(), [ValidRule()]);

    private static SetMaterialScoreRules.RuleInput ValidRule() =>
        new(ElectronicMaterial.Battery, 10.50m, MaterialScoreUnit.PerUnit);
}
