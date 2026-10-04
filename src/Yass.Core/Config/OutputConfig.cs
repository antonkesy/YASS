using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public enum PdfCompression
{
    Jpeg = 0,
    Lossless,
}

/// <summary><c>output:</c> section: how a document is written. Where it goes is the browser's business, as a download.</summary>
public sealed record OutputConfig
{
    [Required]
    public PdfConfig Pdf { get; init; } = new();
}

/// <summary><c>output.pdf:</c> section.</summary>
public sealed record PdfConfig
{
    public PdfCompression Compression { get; init; } = PdfCompression.Jpeg;

    [Range(1, 100)]
    public int JpegQuality { get; init; } = 75;
}
