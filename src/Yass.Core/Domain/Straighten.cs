using System.Globalization;

namespace Yass.Core.Domain;

/// <summary>Fine rotation for a page that went through crooked.</summary>
public static class Straighten
{
    /// <summary>The furthest a page can be straightened either way, in degrees.</summary>
    public const double MaxDegrees = 15;

    /// <summary>
    /// A requested angle rounded to the tenth of a degree the slider steps in, so two clients
    /// asking for nearly the same angle share one hash and one rendered image.
    /// </summary>
    public static double Normalise(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees) || Math.Abs(degrees) > MaxDegrees)
        {
            throw new ArgumentException($"Skew must be between -{MaxDegrees} and {MaxDegrees} degrees, was {degrees.ToString(CultureInfo.InvariantCulture)}.");
        }

        double rounded = Math.Round(degrees, 1, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded;
    }

    /// <summary>
    /// How much a page turned by <paramref name="degrees"/> has to be scaled down for its own
    /// aspect ratio to fit inside the turned page, so cropping to that leaves no empty corners.
    /// </summary>
    public static double InnerScale(double width, double height, double degrees)
    {
        double theta = Math.Abs(degrees) * Math.PI / 180;
        double cos = Math.Cos(theta);
        double sin = Math.Sin(theta);
        return Math.Min(width / ((width * cos) + (height * sin)), height / ((width * sin) + (height * cos)));
    }
}
