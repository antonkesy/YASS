using Tapper;

namespace Yawss.Core.Domain;

/// <summary>Colour mode of a scan or of a per-page override.</summary>
[TranspilationSource]
public enum ColorMode
{
    Bw = 0,
    Grayscale,
    Color,
}
