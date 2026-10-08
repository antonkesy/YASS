using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public enum PdfCompression
{
    Jpeg = 0,
    Lossless,
}

/// <summary>
/// <c>output:</c> section: how a document is written, and the one folder on the server it may
/// also be saved to. Otherwise where it goes is the browser's business, as a download.
/// </summary>
public sealed record OutputConfig
{
    /// <summary>
    /// A folder on the server documents can be saved into — a paperless-ngx consume folder,
    /// say. Empty (the default) turns saving to the server off, and the UI never offers it.
    /// Fixed here and nowhere else: a browser can switch it on for itself, but cannot name
    /// another folder.
    /// </summary>
    [AbsolutePath]
    public string Directory { get; init; } = "";

    /// <summary>Whether <see cref="Directory"/> is set, which is what turns saving to the server on.</summary>
    public bool SavesToServer() => !string.IsNullOrWhiteSpace(Directory);

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
