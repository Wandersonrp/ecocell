using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.CollectorPoints;

/// <summary>Substitui a tabela vigente de pontuação de um Ponto de Coleta.</summary>
public sealed record RequestSetMaterialScoreRulesJson
{
    public List<RequestMaterialScoreRuleJson> Rules { get; init; } = [];
}

/// <summary>Define o valor vigente de um material aceito.</summary>
public sealed record RequestMaterialScoreRuleJson
{
    public ElectronicMaterial Material { get; init; }
    public decimal Points { get; init; }
    public MaterialScoreUnit Unit { get; init; }
}
