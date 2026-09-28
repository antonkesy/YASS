using System.Globalization;
using Tapper;

namespace Yass.Core.Domain;

/// <summary>
/// How much of a page to trim, as a percentage of its width or height from each edge.
/// Percentages rather than pixels so a crop survives a re-render at another resolution.
/// </summary>
[TranspilationSource]
public sealed record CropBox(double Top, double Right, double Bottom, double Left)
{
    /// <summary>Largest total trim allowed on one axis; a crop must leave something behind.</summary>
    public const double MaxTotalPercent = 95;

    /// <summary>A method, not a property: a property would be transpiled into the wire type.</summary>
    public bool IsEmpty() => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;

    /// <exception cref="ArgumentException">An edge is negative or the box would leave nothing.</exception>
    public void Validate()
    {
        foreach ((string name, double value) in new[] { (nameof(Top), Top), (nameof(Right), Right), (nameof(Bottom), Bottom), (nameof(Left), Left) })
        {
            if (double.IsNaN(value) || value < 0 || value >= 100)
            {
                throw new ArgumentException($"Crop {name} must be between 0 and 100, was {value.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        if (Top + Bottom > MaxTotalPercent || Left + Right > MaxTotalPercent)
        {
            throw new ArgumentException($"Crop must leave at least {100 - MaxTotalPercent}% of the page on each axis.");
        }
    }

    /// <summary>The crop as a pixel rectangle within an image of the given size.</summary>
    public (int X, int Y, int Width, int Height) ToPixels(int width, int height)
    {
        int x = (int)Math.Round(width * Left / 100);
        int y = (int)Math.Round(height * Top / 100);
        int w = Math.Max(1, (int)Math.Round(width * (100 - Left - Right) / 100));
        int h = Math.Max(1, (int)Math.Round(height * (100 - Top - Bottom) / 100));
        return (x, y, Math.Min(w, width - x), Math.Min(h, height - y));
    }
}
