using System.ComponentModel.DataAnnotations;
using Yass.Core.Domain;

namespace Yass.Core.Config;

/// <summary>auto: SANE on Linux, WIA on Windows, eSCL on macOS and Android (NAPS2's Apple driver needs a macOS-only build, SPEC.md §6).</summary>
public enum ScannerDriver
{
    Auto = 0,
    Sane,
    Escl,
    Wia,
}

public sealed record ScannerConfig
{
    public ScannerDriver Driver { get; init; } = ScannerDriver.Auto;

    /// <summary>NAPS2 device id or substring; empty = first device found.</summary>
    public string Device { get; init; } = "";

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 5;

    [Required]
    public ScanOptions Defaults { get; init; } = new();

    /// <summary>Deprecated and ignored; still accepted so older yass.yaml files keep loading.</summary>
    [ScanOptionKeys]
    public string[] UiEditable { get; init; } =
        ["source", "dpi", "color", "page_size", "blank_page_detection", "device"];
}
