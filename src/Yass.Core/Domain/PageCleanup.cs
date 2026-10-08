using Tapper;

namespace Yass.Core.Domain;

/// <summary>
/// Clean-up passes a page can have on top of whatever filter it uses. Flags, because they
/// combine with each other and with every filter.
/// </summary>
[TranspilationSource]
[Flags]
public enum PageCleanup
{
    None = 0,

    /// <summary>Remove dust and specks: a small median filter.</summary>
    Despeckle = 1,

    /// <summary>Stretch the page's tones so its darkest ink is black and its paper white.</summary>
    AutoLevels = 2,
}
