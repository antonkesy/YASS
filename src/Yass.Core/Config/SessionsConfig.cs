using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

/// <summary><c>sessions:</c> section.</summary>
public sealed record SessionsConfig
{
    [Required]
    public string Directory { get; init; } = "/data/sessions";

    /// <summary>Sessions older than this are deleted; 0 disables cleanup.</summary>
    [Range(0, 8760)]
    public int RetentionHours { get; init; } = 24;
}
