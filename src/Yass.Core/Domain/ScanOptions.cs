using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Domain;

public sealed record ScanOptions
{
    public ScanSource Source { get; init; } = ScanSource.Feeder;

    [Range(50, 1200)]
    public int Dpi { get; init; } = 300;

    public ColorMode Color { get; init; } = ColorMode.Grayscale;

    public PageSize PageSize { get; init; } = PageSize.A4;

    [Range(-100, 100)]
    public int Brightness { get; init; }

    [Range(-100, 100)]
    public int Contrast { get; init; }

    public bool BlankPageDetection { get; init; } = true;

    [Range(0.0, 1.0)]
    public double BlankPageThreshold { get; init; } = 0.02;

    public bool AutoDeskew { get; init; }

    /// <summary>Empty means whatever <c>scanner.device</c> resolves to.</summary>
    public string Device { get; init; } = "";
}
