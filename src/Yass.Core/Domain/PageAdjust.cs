using System.Globalization;
using Tapper;

namespace Yass.Core.Domain;

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
    // Same mapping as the web preview, so the dialog shows what is rendered.
    public static float Tone(int value) => 1 + (value / 200f);
}
