using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Yass.Core.Domain;

/// <summary>Derived-image cache keys. A page's <see cref="Page.Hash"/> is a pure function of its source bytes and its render-affecting edits.</summary>
public static class PageHash
{
    private const int Length = 16;

    public static string OfBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..Length].ToLowerInvariant();

    /// <summary>Hash for rendering <paramref name="sourceHash"/> with the given edits. Every input that changes a pixel belongs here.</summary>
    public static string Derive(string sourceHash, RenderSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        CropBox? crop = spec.Crop;
        string box = crop is null || crop.IsEmpty()
            ? "-"
            : string.Create(CultureInfo.InvariantCulture, $"{crop.Top}/{crop.Right}/{crop.Bottom}/{crop.Left}");
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{sourceHash}|{(int)spec.Rotation}|{(int)spec.Filter}|{(int)spec.ColorMode}|{box}");
        return OfBytes(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>Recomputes <see cref="Page.Hash"/> from the page's current edits and the session's scan colour mode.</summary>
    public static Page Rehash(Page page, ColorMode sessionColor)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page with { Hash = Derive(page.SourceHash, RenderSpec.Of(page, sessionColor)) };
    }
}
