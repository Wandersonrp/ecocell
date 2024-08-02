using Ecocell.Communication.Enums.Document;
using Ecocell.Communication.Enums.User;
using Ecocell.Communication.Requests.Address;
using Ecocell.Communication.Requests.Document;
using Ecocell.Communication.Requests.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ecocell.Razor.Components.RegisterForm.LegalPerson.Steps;

public partial class StepOne
{
    private MudForm _formStepOne = new();       
    private RequestDocumentJson _document = new();
    private CompanyHierarchy _companyHierarchy { get; set; } = CompanyHierarchy.Headquarter;
    private bool _isLoading { get; set; } 
    private bool _disableCnpj { get; set; } = false;
    private bool _error { get; set; }

    [Parameter]
    public RequestAddressJson Address { get; set; } = new();

    [Parameter]
    public EventCallback<RequestAddressJson> AddressChanged { get; set; }

    [Parameter]
    public RequestRegisterUserJson User { get; set; } = new();

    [Parameter]
    public EventCallback<RequestRegisterUserJson> UserChanged { get; set; }

    //private PatternMask _cnpjMask = new("XX.XXX.XXX/XXXX-XX")
    //{
    //    MaskChars = new[] { new MaskChar('X', @"[0-9]") },
    //    Placeholder = '_',
    //    CleanDelimiters = true,
    //};

    private async Task GetCompanyData()
    {
        if(string.IsNullOrWhiteSpace(_document.Text))
        {
            return;
        }
        
        if(!string.IsNullOrWhiteSpace(User.Email) || !string.IsNullOrWhiteSpace(User.CorporateName))
        {
            return;
        }
        
        _isLoading = true;

        var response = await _legalPersonChecker.GetCompanyData(_document.Text);

        if(response is null)
        {
            _error = true;

            ShowErrorSnackbar("Erro ao buscar dados do CNPJ");
            
            _isLoading = false;

            return;
        }
        
        User.Document = new RequestDocumentJson
        {
            Text = _document.Text,
            DocumentType = DocumentType.CNPJ,
        };

        User.Email = response?.Email?.ToLower() ?? string.Empty;
        User.UserType = UserType.LegalPerson;
        User.Name = response?.Name ?? string.Empty;
        User.Phone = response?.Phone1 ?? string.Empty;        
        User.CorporateName = response?.CorporateName ?? string.Empty;
        User.PrincipalCnae = response?.CnaeFiscalDescription ?? string.Empty;
        User.CompanyHierarchy = response?.CompanyHierarchyDescription is not null ? HandleCompanyHierarchy(response?.CompanyHierarchyDescription!) : CompanyHierarchy.Headquarter;
        User.CompanyStartDate = response?.CompanyStatusDate;
        User.CompanyStatus = response?.CompanyStatus is not null ? HandleCompanyStatus(response.CompanyStatus.ToString()!) : null;


        await OnUserChanged(User);

        var address = new RequestAddressJson
        {
            Street = response?.Street ?? string.Empty,
            Number = response?.Number ?? string.Empty,
            Complement = response?.Complement ?? string.Empty,
            Neighborhood = response?.Neightboorhood ?? string.Empty,
            City = response?.City ?? string.Empty,
            State = response?.State ?? string.Empty,
            ZipCode = response?.ZipCode is not null ? response.ZipCode.ToString()! : string.Empty,
        };

        await OnAddressChanged(address);

        _isLoading = false;

        StateHasChanged();
    }

    private async Task OnUserChanged(RequestRegisterUserJson user)
    {
        User = user;

        await UserChanged.InvokeAsync(User);
    }

    private async Task OnAddressChanged(RequestAddressJson address)
    {
        Address.Street = address.Street;
        Address.Number = address.Number;
        Address.Complement = address.Complement;
        Address.Neighborhood = address.Neighborhood;
        Address.City = address.City;
        Address.State = address.State;
        Address.ZipCode = address.ZipCode;
        Address.Country = address.Country;
        Address.Latitude = address.Latitude;
        Address.Longitude = address.Longitude;
        
        await AddressChanged.InvokeAsync(address);
    }

    private void ShowErrorSnackbar(string message)
    {
        Snackbar.Clear();
        Snackbar.Configuration.PositionClass = Defaults.Classes.Position.TopEnd;
        Snackbar.Configuration.VisibleStateDuration = 1000;
        Snackbar.Add(message, Severity.Error, c =>  c.SnackbarVariant = Variant.Text);
    }

    private CompanyHierarchy HandleCompanyHierarchy(string companyHierarchy)
    {
        switch (companyHierarchy)
        {
            case "MATRIZ":
                return CompanyHierarchy.Headquarter;
            case "FILIAL":
                return CompanyHierarchy.Branch;
            default:
                return CompanyHierarchy.Headquarter;
        }
    }

    private CompanyStatus HandleCompanyStatus(string companyStatus)
    {
        switch(companyStatus)
        {
            case "Ativa":
                return CompanyStatus.Active;

            default:
                return CompanyStatus.Active;

        }
    }
}
