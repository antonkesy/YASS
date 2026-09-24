namespace Yawss.Core.Domain;

/// <summary>Clockwise rotation applied to a page, in degrees.</summary>
public enum Rotation
{
    R0 = 0,
    R90 = 90,
    R180 = 180,
    R270 = 270,
}

public static class RotationExtensions
{
    /// <summary>Adds <paramref name="degrees"/> (any multiple of 90, positive or negative) and normalises to 0..270.</summary>
    public static Rotation Rotate(this Rotation rotation, int degrees)
    {
        if (degrees % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Rotation must be a multiple of 90 degrees.");
        }

        int normalised = (((int)rotation + degrees) % 360 + 360) % 360;
        return (Rotation)normalised;
    }
}
