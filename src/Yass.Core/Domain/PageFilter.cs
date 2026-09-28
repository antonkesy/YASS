using Tapper;

namespace Yass.Core.Domain;

/// <summary>
/// How a page is rendered, per page. Supersedes a plain colour override: the scan's own
/// colour mode stays the baseline (<see cref="Original"/>), and <see cref="Enhance"/> is a
/// treatment rather than a colour mode, so the two cannot be expressed by one enum.
/// </summary>
[TranspilationSource]
public enum PageFilter
{
    /// <summary>Render in the colour mode the page was scanned in.</summary>
    Original = 0,

    /// <summary>Scan colour, pushed for legibility: more contrast, more saturation, slightly brighter.</summary>
    Enhance,

    Grayscale,

    /// <summary>Black and white, thresholded.</summary>
    Bw,
}

public static class PageFilters
{
    /// <summary>Contrast, saturation and brightness for <see cref="PageFilter.Enhance"/>, as the design specifies them.</summary>
    public const float EnhanceContrast = 1.25f;
    public const float EnhanceSaturation = 1.2f;
    public const float EnhanceBrightness = 1.03f;

    /// <summary>The colour mode a filter renders in, given what the page was scanned in.</summary>
    public static ColorMode ColorFor(this PageFilter filter, ColorMode scanned) => filter switch
    {
        PageFilter.Grayscale => ColorMode.Grayscale,
        PageFilter.Bw => ColorMode.Bw,
        _ => scanned,
    };

    public static bool Enhances(this PageFilter filter) => filter == PageFilter.Enhance;
}
