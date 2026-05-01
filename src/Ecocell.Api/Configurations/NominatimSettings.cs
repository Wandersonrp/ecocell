using System.ComponentModel.DataAnnotations;

namespace Ecocell.Api.Configurations;

public sealed class NominatimSettings
{
    public const string SectionName = "Nominatim";

    [Required(ErrorMessage = "URL base do Nominatim é obrigatória.")]
    public string BaseUrl { get; init; } = string.Empty;

    [Required(ErrorMessage = "User-Agent do Nominatim é obrigatório.")]
    public string UserAgent { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 5;
}