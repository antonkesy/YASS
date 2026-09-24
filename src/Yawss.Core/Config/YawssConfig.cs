using System.ComponentModel.DataAnnotations;

namespace Yawss.Core.Config;

/// <summary>Root of <c>yawss.yaml</c>. Property names map to snake_case keys (<see cref="ConfigKeys.ToSnakeCase"/>).</summary>
public sealed record YawssConfig
{
    [Required]
    public ServerConfig Server { get; init; } = new();

    [Required]
    public ScannerConfig Scanner { get; init; } = new();

    [Required]
    public OutputConfig Output { get; init; } = new();

    [Required]
    public ProcessingConfig Processing { get; init; } = new();

    [Required]
    public SessionsConfig Sessions { get; init; } = new();

    [Required]
    public LoggingConfig Logging { get; init; } = new();
}
