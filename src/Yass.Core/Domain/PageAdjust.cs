using System.Globalization;
using Tapper;

namespace Yass.Core.Domain;

/// <summary>
/// Tone and sharpness a page is fine-tuned with after scanning, on top of its filter. Unlike the
/// scan-time brightness and contrast, this works on a page already scanned and can be undone.
/// </summary>
/// <param name="Brightness">-100..100; 0 leaves the page as it is.</param>
/// <param name="Contrast">-100..100; 0 leaves the page as it is.</param>
/// <param name="Sharpen">0..100; 0 leaves the page as it is.</param>
[TranspilationSource]
public sealed record PageAdjust(int Brightness, int Contrast, int Sharpen)
{
    public const int ToneRange = 100;
    public const int MaxSharpen = 100;

    // Methods rather than properties, so the transpiler leaves them off the wire type.
    public bool IsNeutral() => Brightness == 0 && Contrast == 0 && Sharpen == 0;

    public void Validate()
    {
        Check(nameof(Brightness), Brightness, -ToneRange, ToneRange);
        Check(nameof(Contrast), Contrast, -ToneRange, ToneRange);
        Check(nameof(Sharpen), Sharpen, 0, MaxSharpen);
    }

    private static void Check(string name, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new ArgumentException($"Adjust {name} must be between {min} and {max}, was {value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}

public static class PageAdjusts
{
    /// <summary>
    /// A slider value as a CSS-style factor: 1 is unchanged, -100 halves, 100 is one and a half.
    /// The same mapping the web preview uses, so what the dialog shows is what is rendered.
    /// </summary>
    public static float Tone(int value) => 1 + (value / 200f);
}
