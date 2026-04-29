using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

/// <summary>
/// Entidade que representa uma pessoa jurídica no sistema, com dados de registro legal,
/// CNPJ, razão social e relação com responsável (pessoa física gestora).
/// </summary>
public class LegalPerson : Person
{
    public string LegalName { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public string Cnpj { get; private set; } = string.Empty;
    public string? Cnae { get; private set; }

    public Guid? ResponsiblePersonId { get; private set; }
    public virtual NaturalPerson? ResponsiblePerson { get; private set; }

    public Guid? AddressId { get; private set; }
    public virtual Address? Address { get; private set; }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="LegalPerson"/> com os dados de registro e vínculo opcionais.
    /// </summary>
    /// <param name="legalName">Razão social da pessoa jurídica.</param>
    /// <param name="tradeName">Nome fantasia ou comercial.</param>
    /// <param name="cnpj">CNPJ sem formatação.</param>
    /// <param name="email">Endereço de e-mail de contato.</param>
    /// <param name="journey">Jornada no sistema (Depositante, Ponto de Coleta ou Coletor).</param>
    /// <param name="addressId">Identificador da localização associada (opcional).</param>
    /// <param name="responsiblePersonId">Identificador da pessoa física responsável (opcional).</param>
    /// <param name="cnae">Código da Classificação Nacional de Atividades Econômicas (opcional).</param>
    public LegalPerson(
        string legalName,
        string tradeName,
        string cnpj,
        string email,
        Journey journey,
        Guid? addressId = null,
        Guid? responsiblePersonId = null,
        string? cnae = null)
    {
        LegalName = legalName;
        TradeName = tradeName;
        Email = email;
        Cnpj = cnpj;
        AddressId = addressId;
        ResponsiblePersonId = responsiblePersonId;
        Role = Role.User;
        Journey = journey;
        PersonType = PersonType.LegalPerson;
        Cnae = cnae;

        if (journey == Journey.CollectPoint || journey == Journey.Collector)
            PersonStatus = PersonStatus.PendingApproval;
    }
}
