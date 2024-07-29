using Microsoft.AspNetCore.Components;
using Ecocell.Communication.Requests.Address;
using MudBlazor;

namespace Ecocell.Razor.Components.RegisterForm.LegalPerson.Steps;

public partial class StepTwo
{
    private MudForm _formStepTwo = new();
    private bool _error;

    [Parameter]
    public RequestAddressJson Address { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        if(!string.IsNullOrEmpty(Address.ZipCode))
        {
            await GetGeolocation(Address.ZipCode);
        }
    }

    private PatternMask _zipCodeMask = new PatternMask("XXXXX-XXX")
    {
        MaskChars = new[] { new MaskChar('X', @"[0-9]") },
        Placeholder = '_',
        CleanDelimiters = true,
    };

    private async Task GetGeolocation(string zipCode)
    {
        var response = await _geocoding.GetGeocodingByPostalCode(zipCode);

        if(response is null)
        {
            _error = true;
            return;
        }

        var latitude = response.Roots.FirstOrDefault()?.Latitude;

        var longitude = response.Roots.FirstOrDefault()?.Longitude;

        Address.Latitude = latitude ?? string.Empty;

        Address.Longitude = longitude ?? string.Empty;
    }
}
