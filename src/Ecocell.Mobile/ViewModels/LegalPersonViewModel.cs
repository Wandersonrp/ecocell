using System.Net;
using System.Text.RegularExpressions;
using Ecocell.Mobile.Services.Api;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests;

namespace Ecocell.Mobile.ViewModels;

/// <summary>
/// Estado e orquestração do cadastro de Ponto de Coleta (wizard de 2 passos).
/// Journey é fixada em <see cref="Journey.CollectPoint"/> (RN003).
/// </summary>
public sealed class LegalPersonViewModel
{
    private readonly ILegalPersonClient _client;

    public LegalPersonViewModel(ILegalPersonClient client) => _client = client;

    // Passo 1 — empresa
    public string Cnpj { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Passo 2 — endereço
    public string ZipCode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Complement { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;

    public Dictionary<string, string> Errors { get; } = new();
    public bool IsLoading { get; private set; }
    public string? BannerMessage { get; private set; }

    private static string Digits(string v) => Regex.Replace(v, @"\D", "");

    public bool ValidateStep1()
    {
        Errors.Clear();
        if (Digits(Cnpj).Length != 14) Errors["cnpj"] = "Informe um CNPJ válido.";
        if (string.IsNullOrWhiteSpace(LegalName)) Errors["legalName"] = "Informe a razão social.";
        if (string.IsNullOrWhiteSpace(TradeName)) Errors["tradeName"] = "Informe o nome fantasia.";
        if (!Regex.IsMatch(Email.Trim(), @"\S+@\S+\.\S+")) Errors["email"] = "Informe um e-mail válido.";
        return Errors.Count == 0;
    }

    public bool ValidateStep2()
    {
        Errors.Clear();
        if (Digits(ZipCode).Length != 8) Errors["zipCode"] = "Informe um CEP válido.";
        if (string.IsNullOrWhiteSpace(Street)) Errors["street"] = "Informe o logradouro.";
        if (string.IsNullOrWhiteSpace(Number)) Errors["number"] = "Informe o número.";
        if (string.IsNullOrWhiteSpace(Neighborhood)) Errors["neighborhood"] = "Informe o bairro.";
        if (string.IsNullOrWhiteSpace(City)) Errors["city"] = "Informe a cidade.";
        if (string.IsNullOrWhiteSpace(Uf)) Errors["uf"] = "Selecione a UF.";
        return Errors.Count == 0;
    }

    /// <summary>Valida o passo 2 e envia o cadastro. Em falha, popula <see cref="Errors"/> ou <see cref="BannerMessage"/>.</summary>
    public async Task<bool> SubmitAsync(CancellationToken ct = default)
    {
        BannerMessage = null;
        if (!ValidateStep2()) return false;

        IsLoading = true;
        try
        {
            var request = new RequestRegisterLegalPerson
            {
                Cnpj = Digits(Cnpj),
                LegalName = LegalName.Trim(),
                TradeName = TradeName.Trim(),
                Email = Email.Trim(),
                Journey = Journey.CollectPoint,
                Address = new RequestRegisterLegalPersonAddress
                {
                    Street = Street.Trim(),
                    Number = Number.Trim(),
                    Complement = string.IsNullOrWhiteSpace(Complement) ? null : Complement.Trim(),
                    Neighborhood = Neighborhood.Trim(),
                    City = City.Trim(),
                    State = Uf,
                    ZipCode = Digits(ZipCode),
                },
            };

            var response = await _client.RegisterAsync(request, ct);
            if (response.IsSuccessStatusCode) return true;

            BannerMessage = response.StatusCode == HttpStatusCode.Conflict
                ? "CNPJ ou e-mail já cadastrado."
                : "Verifique os dados e tente novamente.";
            return false;
        }
        catch (Exception)
        {
            BannerMessage = "Não foi possível enviar. Tente novamente.";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
