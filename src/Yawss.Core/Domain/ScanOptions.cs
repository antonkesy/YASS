namespace Yawss.Core.Domain;

/// <summary>Options a scan is started with. Also the shape of <c>scanner.defaults</c> in the config file.</summary>
public sealed record ScanOptions
{
    public ScanSource Source { get; init; } = ScanSource.Feeder;

    public int Dpi { get; init; } = 300;

    public ColorMode Color { get; init; } = ColorMode.Grayscale;

    public PageSize PageSize { get; init; } = PageSize.A4;

    public int Brightness { get; init; }

    public int Contrast { get; init; }

    public bool BlankPageDetection { get; init; } = true;

    public double BlankPageThreshold { get; init; } = 0.02;

    public bool AutoDeskew { get; init; }
}
