using System.Globalization;

namespace Yass.Core.Domain;

public static class Straighten
{
    public const double MaxDegrees = 15;

    // Rounded to the slider's 0.1° step so near-identical angles share one hash and rendered image.
    public static double Normalise(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees) || Math.Abs(degrees) > MaxDegrees)
        {
            throw new ArgumentException($"Skew must be between -{MaxDegrees} and {MaxDegrees} degrees, was {degrees.ToString(CultureInfo.InvariantCulture)}.");
        }

        double rounded = Math.Round(degrees, 1, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded;
    }

    // Scale-down that fits the page's own aspect ratio inside the turned page, so cropping leaves no empty corners.
    public static double InnerScale(double width, double height, double degrees)
    {
        double theta = Math.Abs(degrees) * Math.PI / 180;
        double cos = Math.Cos(theta);
        double sin = Math.Sin(theta);
        return Math.Min(width / ((width * cos) + (height * sin)), height / ((width * sin) + (height * cos)));
    }
}
