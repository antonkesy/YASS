using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public enum PdfCompression
{
    Jpeg = 0,
    Lossless,
}

public sealed record OutputConfig
{
    /// <summary>Empty turns saving to the server off. Only set here: a browser can opt in but never name another folder.</summary>
    [AbsolutePath]
    public string Directory { get; init; } = "";

    public bool SavesToServer() => !string.IsNullOrWhiteSpace(Directory);

    [Required]
    public PdfConfig Pdf { get; init; } = new();
}

public sealed record PdfConfig
{
    public PdfCompression Compression { get; init; } = PdfCompression.Jpeg;

    [Range(1, 100)]
    public int JpegQuality { get; init; } = 75;
}
