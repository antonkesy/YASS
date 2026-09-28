using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

/// <summary><c>processing:</c> section.</summary>
public sealed record ProcessingConfig
{
    /// <summary>Upper bound on decoded bitmaps held in memory at once.</summary>
    [Range(1, 16)]
    public int MaxConcurrentRenders { get; init; } = 2;

    [Range(64, 1024)]
    public int ThumbnailWidth { get; init; } = 320;

    [Range(320, 4096)]
    public int PreviewMaxWidth { get; init; } = 1600;
}
