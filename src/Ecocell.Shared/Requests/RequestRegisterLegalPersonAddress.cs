namespace Ecocell.Shared.Requests;

/// <summary>
/// Dados de endereço para o cadastro de pessoa jurídica.
/// </summary>
public record RequestRegisterLegalPersonAddress
{
    /// <summary>Logradouro.</summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>Número.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Complemento (opcional).</summary>
    public string? Complement { get; set; }

    /// <summary>Bairro.</summary>
    public string Neighborhood { get; set; } = string.Empty;

    /// <summary>Cidade.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Sigla do estado (2 caracteres).</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>CEP sem máscara (8 dígitos).</summary>
    public string ZipCode { get; set; } = string.Empty;
}