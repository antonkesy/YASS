using System.ComponentModel.DataAnnotations;
using Yass.Core.Domain;

namespace Yass.Core.Config;

public enum ScannerDriver
{
    Auto = 0,
    Sane,
    Escl,
}

/// <summary><c>scanner:</c> section.</summary>
public sealed record ScannerConfig
{
    public ScannerDriver Driver { get; init; } = ScannerDriver.Auto;

    /// <summary>NAPS2 device id or substring; empty = first device found.</summary>
    public string Device { get; init; } = "";

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 5;

    [Required]
    public ScanOptions Defaults { get; init; } = new();

    /// <summary>
    /// Deprecated and ignored: every scan option can be changed from the UI, and this file only
    /// supplies the defaults. Still accepted so a yass.yaml written when it mattered keeps loading.
    /// </summary>
    [ScanOptionKeys]
    public string[] UiEditable { get; init; } =
        ["source", "dpi", "color", "page_size", "blank_page_detection", "device"];
}
