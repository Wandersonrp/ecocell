using System.ComponentModel.DataAnnotations;

namespace Ecocell.Api.Configurations;

/// <summary>
/// Configurações de conexão com o Redis, lidas da seção "Redis" do appsettings.
/// </summary>
public class RedisSettings
{
    public const string SectionName = "Redis";

    /// <summary>
    /// String de conexão do Redis (ex.: "localhost:6379").
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
