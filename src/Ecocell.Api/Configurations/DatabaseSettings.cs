using System.ComponentModel.DataAnnotations;

namespace Ecocell.Api.Configurations;

public sealed class DatabaseSettings
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "A string de conexão é obrigatória.")]
    public string DefaultConnection { get; init; } = string.Empty;
}
 