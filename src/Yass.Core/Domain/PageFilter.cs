using Tapper;

namespace Yass.Core.Domain;

[TranspilationSource]
public enum PageFilter
{
    Original = 0,

    Enhance,

    Grayscale,

    Bw,
}

public static class PageFilters
{
    public const float EnhanceContrast = 1.25f;
    public const float EnhanceSaturation = 1.2f;
    public const float EnhanceBrightness = 1.03f;

    public static ColorMode ColorFor(this PageFilter filter, ColorMode scanned) => filter switch
    {
        PageFilter.Grayscale => ColorMode.Grayscale,
        PageFilter.Bw => ColorMode.Bw,
        _ => scanned,
    };

    public static bool Enhances(this PageFilter filter) => filter == PageFilter.Enhance;
}
