using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Yass.Core.Domain;

public static class PageHash
{
    private const int Length = 16;

    public static string OfBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..Length].ToLowerInvariant();

    /// <summary>Every input that changes a pixel belongs here.</summary>
    public static string Derive(string sourceHash, RenderSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        CropBox? crop = spec.Crop;
        string box = crop is null || crop.IsEmpty()
            ? "-"
            : string.Create(CultureInfo.InvariantCulture, $"{crop.Top}/{crop.Right}/{crop.Bottom}/{crop.Left}");
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{sourceHash}|{(int)spec.Rotation}|{(int)spec.Filter}|{(int)spec.ColorMode}|{box}{Extras(spec)}");
        return OfBytes(Encoding.UTF8.GetBytes(key));
    }

    // Newer edits are appended only when set, so pages without them keep their old hash and cached images.
    private static string Extras(RenderSpec spec)
    {
        var extras = new StringBuilder();
        if (spec.Adjust is { } a && !a.IsNeutral())
        {
            extras.Append(CultureInfo.InvariantCulture, $"|a{a.Brightness}/{a.Contrast}/{a.Sharpen}");
        }

        if (spec.Cleanup != PageCleanup.None)
        {
            extras.Append(CultureInfo.InvariantCulture, $"|c{(int)spec.Cleanup}");
        }

        if (spec.Skew != 0)
        {
            extras.Append(CultureInfo.InvariantCulture, $"|k{spec.Skew:0.0}");
        }

        return extras.ToString();
    }

    public static Page Rehash(Page page, ColorMode sessionColor)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page with { Hash = Derive(page.SourceHash, RenderSpec.Of(page, sessionColor)) };
    }
}
