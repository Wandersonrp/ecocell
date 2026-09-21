using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição para cadastro de uma nova pessoa jurídica (Ponto de Coleta ou Coletor).
/// </summary>
public record RequestRegisterLegalPerson
{
    /// <summary>CNPJ da empresa (14 dígitos, sem máscara).</summary>
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Razão social.</summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>Nome fantasia.</summary>
    public string TradeName { get; set; } = string.Empty;

    /// <summary>E-mail de contato da empresa.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Tipo de jornada (apenas CollectPoint ou Collector).</summary>
    public Journey Journey { get; set; }

    /// <summary>Endereço da sede da empresa.</summary>
    public RequestRegisterLegalPersonAddress Address { get; set; } = new();
}