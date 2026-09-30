using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Domain;

/// <summary>Options a scan is started with. Also the shape of <c>scanner.defaults</c> in the config file.</summary>
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

    /// <summary>Fraction of dark pixels below which a page counts as blank.</summary>
    [Range(0.0, 1.0)]
    public double BlankPageThreshold { get; init; } = 0.02;

    public bool AutoDeskew { get; init; }

    /// <summary>
    /// Which scanner to use, by the id <see cref="Yass.Core.Scanning.ScannerDevice"/> reports.
    /// Empty means whatever <c>scanner.device</c> resolves to, which is the only behaviour there
    /// was before a browser could choose.
    /// </summary>
    public string Device { get; init; } = "";
}
