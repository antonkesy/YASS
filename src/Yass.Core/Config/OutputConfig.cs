using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

public enum PdfCompression
{
    Jpeg = 0,
    Lossless,
}

public enum OnSuccessAction
{
    KeepSession = 0,
    DeleteSession,
}

/// <summary><c>output:</c> section.</summary>
public sealed record OutputConfig
{
    /// <summary>Where finished PDFs are written, typically the paperless-ngx consume directory.</summary>
    [Required]
    public string Directory { get; init; } = "/data/consume";

    [Required]
    [RegularExpression(@"^.+\.pdf$", ErrorMessage = "must end with .pdf")]
    public string FilenameTemplate { get; init; } = "{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf";

    [Required]
    public PdfConfig Pdf { get; init; } = new();

    /// <summary>Offer a browser download instead of only the server-side save.</summary>
    public bool AllowDownload { get; init; } = true;

    public OnSuccessAction OnSuccess { get; init; } = OnSuccessAction.KeepSession;
}

/// <summary><c>output.pdf:</c> section.</summary>
public sealed record PdfConfig
{
    public PdfCompression Compression { get; init; } = PdfCompression.Jpeg;

    [Range(1, 100)]
    public int JpegQuality { get; init; } = 75;
}
