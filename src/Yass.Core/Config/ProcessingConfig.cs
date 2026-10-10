using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public sealed record ProcessingConfig
{
    [Range(1, 16)]
    public int MaxConcurrentRenders { get; init; } = 2;

    [Range(64, 1024)]
    public int ThumbnailWidth { get; init; } = 320;

    [Range(320, 4096)]
    public int PreviewMaxWidth { get; init; } = 1600;
}
