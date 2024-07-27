using System.Text.Json.Serialization;

namespace Ecocell.Communication.Responses.BrazilApiCompanyData;

public record ResponseBrazilApiCompanyDataJson
{
    [JsonPropertyName("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    [JsonPropertyName("identificador_matriz_filial")]
    public int IdentifierCompanyHierarchy { get; set; }

    [JsonPropertyName("descricao_matriz_filial")]
    public string CompanyHierarchyDescription { get; set; } = string.Empty;

    [JsonPropertyName("razao_social")]
    public string CorporateName { get; set; } = string.Empty;

    [JsonPropertyName("nome_fantasia")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("situacao_cadastral")]
    public int CompanyStatus { get; set; }

    [JsonPropertyName("descricao_situacao_cadastral")]
    public string CompanyStatusDescription { get; set; } = string.Empty;

    [JsonPropertyName("data_situacao_cadastral")]
    public DateOnly CompanyStatusDate { get; set; }

    [JsonPropertyName("motivo_situacao_cadastral")]
    public int CompanyStatusReason { get; set; }

    [JsonPropertyName("nome_cidade_exterior")]
    public string? ExternalCityName { get; set; }

    [JsonPropertyName("codigo_natureza_juridica")]
    public int LegalCode { get; set; }

    [JsonPropertyName("data_inicio_atividade")]
    public DateOnly InitialActivityDate { get; set; }

    [JsonPropertyName("cnae_fiscal")]
    public int CnaeFiscal { get; set; }

    [JsonPropertyName("cnae_fiscal_descricao")]
    public string? CnaeFiscalDescription { get; set; }

    [JsonPropertyName("descricao_tipo_de_logradouro")]
    public string? StreetTypeDescription { get; set; }

    [JsonPropertyName("logradouro")]
    public string? Street { get; set; } 

    [JsonPropertyName("numero")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("complemento")]
    public string? Complement { get; set; }

    [JsonPropertyName("bairro")]
    public string Neightboorhood { get; set; } = string.Empty;

    [JsonPropertyName("cep")]
    public int ZipCode { get; set; }

    [JsonPropertyName("uf")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("codigo_municipio")]
    public int CityCode { get; set; }

    [JsonPropertyName("municipio")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("ddd_telefone_1")]
    public string? Phone1 { get; set; }

    [JsonPropertyName("ddd_telefone_2")]
    public string? Phone2 { get; set; }

    [JsonPropertyName("ddd_fax")]
    public string? Fax { get; set; }

    [JsonPropertyName("qualificacao_do_responsavel")]
    public int ReponsibleQualifier { get; set; }

    [JsonPropertyName("capital_social")]
    public int ShareCapital { get; set; }

    [JsonPropertyName("porte")]
    public int CompanySize { get; set; }

    [JsonPropertyName("descricao_porte")]
    public string? CompanySizeDescription { get; set; }

    [JsonPropertyName("opcao_pelo_simples")]
    public bool SimplesOption { get; set; }

    [JsonPropertyName("data_opcao_pelo_simples")]
    public DateOnly? SimplesOptionDate { get; set; }

    [JsonPropertyName("data_exclusao_do_simples")]
    public DateOnly? SimplesOptionExclusion { get; set; }

    [JsonPropertyName("opcao_pelo_mei")]
    public bool MeiOption { get; set; }

    [JsonPropertyName("situacao_especial")]
    public string? EspecialSituation { get; set; }

    [JsonPropertyName("data_situacao_especial")]
    public DateOnly? EspecialSituationDate { get; set; }

    [JsonPropertyName("cnaes_secundarios")]
    public IEnumerable<SecondaryCnae> SecondariesCnaes { get; set; } = [];

    [JsonPropertyName("qsa")]
    public IEnumerable<Qsa> Qsa { get; set; } = [];
}

public record SecondaryCnae
{
    [JsonPropertyName("codigo")]
    public int Code { get; set; }

    [JsonPropertyName("descricao")]
    public string Description { get; set; } = string.Empty;
}

public record Qsa
{
    [JsonPropertyName("identificador_de_socio")]
    public int PartnerIdentifier { get; set; }

    [JsonPropertyName("nome_socio")]
    public string? PartnerName { get; set; }

    [JsonPropertyName("cnpj_cpf_do_socio")]
    public string? PartnerDocument { get; set; }

    [JsonPropertyName("codigo_qualificacao_do_socio")]
    public int PartnerQualifier { get; set; }

    [JsonPropertyName("percentual_capital_social")]
    public int ShareCapitalPercentage { get; set; }

    [JsonPropertyName("data_entrada_sociedade")]
    public DateOnly? PartnerEntryDate { get; set; }

    [JsonPropertyName("cpf_representante_legal")]
    public string? LegalRepresentativeDocument { get; set; }

    [JsonPropertyName("nome_representante_legal")]
    public string? LegalRepresentativeName { get; set; }

    [JsonPropertyName("codigo_qualificacao_representante_legal")]
    public int LegalRepresentativeQualifierCode { get; set; }

}

