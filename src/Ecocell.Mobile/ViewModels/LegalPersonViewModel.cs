using Ecocell.Mobile.Services.Api;

namespace Ecocell.Mobile.ViewModels;

/// <summary>Orquestra o wizard de cadastro de Ponto de Coleta (PJ).</summary>
public class LegalPersonViewModel
{
    private readonly ILegalPersonClient _legalPersonClient;

    public LegalPersonViewModel(ILegalPersonClient legalPersonClient)
    {
        _legalPersonClient = legalPersonClient;
    }
}
