using System.Text.Json.Serialization;

namespace Ecocell.Communication.Responses.BrazilApiCompanyData;

public record ResponseBrazilApiCompanyDataJson
{
    [JsonPropertyName("cnpj")]
    public string? Cnpj { get; set; } = null!;

    [JsonPropertyName("identificador_matriz_filial")]
    public int? IdentifierCompanyHierarchy { get; set; } = null!;

    [JsonPropertyName("descricao_identificador_matriz_filial")]
    public string? CompanyHierarchyDescription { get; set; } = null!;

    [JsonPropertyName("razao_social")]
    public string? CorporateName { get; set; } = null!;

    [JsonPropertyName("nome_fantasia")]
    public string? Name { get; set; } = null!;

    [JsonPropertyName("email")]
    public string? Email { get; set; } = null!;

    [JsonPropertyName("situacao_cadastral")]
    public int? CompanyStatus { get; set; } = null;

    [JsonPropertyName("descricao_situacao_cadastral")]
    public string? CompanyStatusDescription { get; set; } = null!;

    [JsonPropertyName("data_situacao_cadastral")]
    public DateOnly? CompanyStatusDate { get; set; } = null!;

    [JsonPropertyName("motivo_situacao_cadastral")]
    public int? CompanyStatusReason { get; set; } = null!;

    [JsonPropertyName("nome_cidade_exterior")]
    public string? ExternalCityName { get; set; }

    [JsonPropertyName("codigo_natureza_juridica")]
    public int? LegalCode { get; set; } = null!;

    [JsonPropertyName("data_inicio_atividade")]
    public DateOnly? InitialActivityDate { get; set; } = null!;

    [JsonPropertyName("cnae_fiscal")]
    public int? CnaeFiscal { get; set; } = null!;

    [JsonPropertyName("cnae_fiscal_descricao")]
    public string? CnaeFiscalDescription { get; set; }

    [JsonPropertyName("descricao_tipo_de_logradouro")]
    public string? StreetTypeDescription { get; set; }

    [JsonPropertyName("logradouro")]
    public string? Street { get; set; } 

    [JsonPropertyName("numero")]
    public string? Number { get; set; } = null!;

    [JsonPropertyName("complemento")]
    public string? Complement { get; set; } = null!;

    [JsonPropertyName("bairro")]
    public string? Neightboorhood { get; set; } = null!;

    [JsonPropertyName("cep")]
    public int? ZipCode { get; set; } = null!;

    [JsonPropertyName("uf")]
    public string? State { get; set; } = null!;

    [JsonPropertyName("codigo_municipio")]
    public int? CityCode { get; set; } = null!;

    [JsonPropertyName("municipio")]
    public string? City { get; set; } = null!;

    [JsonPropertyName("ddd_telefone_1")]
    public string? Phone1 { get; set; }

    [JsonPropertyName("ddd_telefone_2")]
    public string? Phone2 { get; set; }

    [JsonPropertyName("ddd_fax")]
    public string? Fax { get; set; }

    [JsonPropertyName("qualificacao_do_responsavel")]
    public int? ReponsibleQualifier { get; set; } = null!;

    [JsonPropertyName("capital_social")]
    public ulong? ShareCapital { get; set; } = null!;

    [JsonPropertyName("porte")]
    public string? CompanySize { get; set; } = null!;

    [JsonPropertyName("descricao_porte")]
    public string? CompanySizeDescription { get; set; } = null!;

    [JsonPropertyName("opcao_pelo_simples")]
    public bool? SimplesOption { get; set; } = null!;

    [JsonPropertyName("data_opcao_pelo_simples")]
    public DateOnly? SimplesOptionDate { get; set; } = null!;

    [JsonPropertyName("data_exclusao_do_simples")]
    public DateOnly? SimplesOptionExclusion { get; set; } = null!;

    [JsonPropertyName("opcao_pelo_mei")]
    public bool? MeiOption { get; set; } = null!;

    [JsonPropertyName("situacao_especial")]
    public string? EspecialSituation { get; set; } = null!;

    [JsonPropertyName("data_situacao_especial")]
    public DateOnly? EspecialSituationDate { get; set; } = null!;

    [JsonPropertyName("cnaes_secundarios")]
    public IEnumerable<SecondaryCnae> SecondariesCnaes { get; set; } = [];

    [JsonPropertyName("qsa")]
    public IEnumerable<Qsa> Qsa { get; set; } = [];
}

public record SecondaryCnae
{
    [JsonPropertyName("codigo")]
    public int? Code { get; set; } = null!;

    [JsonPropertyName("descricao")]
    public string? Description { get; set; } = null!;
}

public record Qsa
{
    [JsonPropertyName("identificador_de_socio")]
    public int? PartnerIdentifier { get; set; } = null!;

    [JsonPropertyName("nome_socio")]
    public string? PartnerName { get; set; } = null!;

    [JsonPropertyName("cnpj_cpf_do_socio")]
    public string? PartnerDocument { get; set; } = null!;

    [JsonPropertyName("codigo_qualificacao_do_socio")]
    public int? PartnerQualifier { get; set; } = null;

    [JsonPropertyName("percentual_capital_social")]
    public int? ShareCapitalPercentage { get; set; } = null!;

    [JsonPropertyName("data_entrada_sociedade")]
    public DateOnly? PartnerEntryDate { get; set; }

    [JsonPropertyName("cpf_representante_legal")]
    public string? LegalRepresentativeDocument { get; set; } = null!;

    [JsonPropertyName("nome_representante_legal")]
    public string? LegalRepresentativeName { get; set; } = null!;

    [JsonPropertyName("codigo_qualificacao_representante_legal")]
    public int? LegalRepresentativeQualifierCode { get; set; } = null!;
}

