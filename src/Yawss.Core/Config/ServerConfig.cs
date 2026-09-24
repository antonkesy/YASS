using System.ComponentModel.DataAnnotations;

namespace Yawss.Core.Config;

/// <summary><c>server:</c> section. Not reloaded at runtime.</summary>
public sealed record ServerConfig
{
    /// <summary>LAN bind address as <c>host:port</c>. There is no authentication; never expose publicly.</summary>
    [Required]
    [RegularExpression(@"^[^\s:/]+:\d{1,5}$", ErrorMessage = "must be host:port")]
    public string Listen { get; init; } = "0.0.0.0:8080";

    [Required]
    [RegularExpression(@"^/[^\s]*$", ErrorMessage = "must start with '/'")]
    public string BasePath { get; init; } = "/";
}
