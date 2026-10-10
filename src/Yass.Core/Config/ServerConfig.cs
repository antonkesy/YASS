using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public sealed record ServerConfig
{
    /// <summary>There is no authentication; never expose publicly.</summary>
    [Required]
    [RegularExpression(@"^[^\s:/]+:\d{1,5}$", ErrorMessage = "must be host:port")]
    public string Listen { get; init; } = "0.0.0.0:8080";

    [Required]
    [RegularExpression(@"^/[^\s]*$", ErrorMessage = "must start with '/'")]
    public string BasePath { get; init; } = "/";
}
