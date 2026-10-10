using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public sealed record SessionsConfig
{
    [Required]
    public string Directory { get; init; } = "/data/sessions";

    /// <summary>0 disables cleanup.</summary>
    [Range(0, 8760)]
    public int RetentionHours { get; init; } = 24;
}
