using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

/// <summary>Versão temporal da regra de pontuação de um material em um Ponto de Coleta.</summary>
public sealed class MaterialScoreRule : BaseEntity
{
    private MaterialScoreRule()
    {
    }

    /// <summary>Cria uma regra aberta com início de vigência em UTC.</summary>
    public MaterialScoreRule(
        Guid legalPersonId,
        ElectronicMaterial material,
        decimal points,
        MaterialScoreUnit unit,
        DateTime validFrom)
    {
        if (legalPersonId == Guid.Empty)
            throw new ArgumentException("O identificador da pessoa jurídica é obrigatório.", nameof(legalPersonId));

        if (!Enum.IsDefined(typeof(ElectronicMaterial), material))
            throw new ArgumentOutOfRangeException(nameof(material), "O material informado é inválido.");

        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "A pontuação deve ser maior que zero.");

        if (!Enum.IsDefined(typeof(MaterialScoreUnit), unit))
            throw new ArgumentOutOfRangeException(nameof(unit), "A unidade de pontuação informada é inválida.");

        EnsureUtc(validFrom, nameof(validFrom));

        LegalPersonId = legalPersonId;
        Material = material;
        Points = points;
        Unit = unit;
        ValidFrom = validFrom;
    }

    public Guid LegalPersonId { get; private set; }
    public LegalPerson LegalPerson { get; private set; } = null!;
    public ElectronicMaterial Material { get; private set; }
    public decimal Points { get; private set; }
    public MaterialScoreUnit Unit { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    /// <summary>Encerra a vigência da regra sem apagar seu histórico.</summary>
    public void Close(DateTime closedAt)
    {
        if (ValidTo is not null)
            throw new InvalidOperationException("A regra de pontuação já está encerrada.");

        EnsureUtc(closedAt, nameof(closedAt));

        if (closedAt <= ValidFrom)
            throw new ArgumentOutOfRangeException(
                nameof(closedAt),
                "O encerramento deve ser posterior ao início da vigência.");

        ValidTo = closedAt;
        MarkAsUpdated();
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A data deve estar em UTC.", parameterName);
    }
}
