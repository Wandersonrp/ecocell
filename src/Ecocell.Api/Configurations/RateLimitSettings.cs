using System.ComponentModel.DataAnnotations;

namespace Ecocell.Api.Configurations;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    [Required]
    public bool Enabled { get; init; } = true;

    [Required]
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 5;

    [Required]
    [Range(1, int.MaxValue)]
    public int WindowMinutes { get; init; } = 1;
}