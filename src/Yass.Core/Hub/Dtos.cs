using Tapper;
using Yass.Core.Domain;
using Yass.Core.Scanning;

namespace Yass.Core.Hub;

/// <summary>Wire shape of a session. Dates are ISO-8601 strings so they survive MessagePack and TypeScript unchanged.</summary>
/// <param name="Id">Session id, also its directory name.</param>
/// <param name="CreatedAt">When the scan was started.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="Pages">Pages in display order, deleted ones included.</param>
/// <param name="OptionsUsed">Effective options the scan ran with.</param>
/// <param name="Error">Why the session failed; otherwise <c>null</c>.</param>
/// <param name="Name">What the user called this scan; null means the UI falls back to the timestamp.</param>
/// <param name="Events">History, oldest first. The last entry is the status the UI shows.</param>
/// <param name="SizeBytes">What the session currently occupies on disk, source pages and caches included.</param>
[TranspilationSource]
public sealed record SessionDto(
    string Id,
    string CreatedAt,
    SessionState State,
    PageDto[] Pages,
    ScanOptionsDto OptionsUsed,
    string? Error,
    string? Name,
    SessionEventDto[] Events,
    long SizeBytes);

[TranspilationSource]
public sealed record SessionEventDto(SessionEventKind Kind, string At, string? Path);

[TranspilationSource]
public sealed record PageDto(
    int Index,
    Rotation Rotation,
    PageFilter Filter,
    CropBox? Crop,
    bool StartsDocument,
    bool Deleted,
    bool IsBlank,
    string Hash,
    bool ThumbnailReady,
    PageAdjust? Adjust,
    PageCleanup Cleanup,
    double Skew);


/// <summary>
/// Scan options on the wire. Every field is optional: as a <c>StartScan</c> argument each field sent
/// overrides the matching <c>scanner.defaults</c> value; as a <c>GetEffectiveOptions</c> result every field
/// is filled in.
/// </summary>
[TranspilationSource]
public sealed record ScanOptionsDto(
    ScanSource? Source = null,
    int? Dpi = null,
    ColorMode? Color = null,
    PageSize? PageSize = null,
    int? Brightness = null,
    int? Contrast = null,
    bool? BlankPageDetection = null,
    double? BlankPageThreshold = null,
    bool? AutoDeskew = null,
    string? Device = null);

[TranspilationSource]
public sealed record ScannerStatusDto(ScannerState State, string? DeviceName, string? Message, string CheckedAt);

/// <summary>One scanner to choose between. <see cref="Id"/> is what a scan asks for.</summary>
/// <param name="Id">The driver's identifier, what a scan asks for.</param>
/// <param name="Name">What the driver calls it.</param>
/// <param name="Driver">The driver that found it — sane, escl, … — when known.</param>
/// <param name="Connection">Where it is reached, e.g. an eSCL address, when the driver reports one.</param>
[TranspilationSource]
public sealed record ScannerDeviceDto(string Id, string Name, string? Driver = null, string? Connection = null);

/// <summary>File type a session is exported as. Images write one file per page.</summary>
[TranspilationSource]
public enum ExportFormat
{
    Pdf = 0,
    Jpg,
    Png,
}

/// <summary>
/// What to produce: a one-shot download URL per document, or with <see cref="ToServer"/>, a file
/// per document in the server's <c>output.directory</c>.
/// </summary>
/// <param name="Format">File type to produce.</param>
/// <param name="Name">Base file name, without extension; empty falls back to the session's name or a generated one.</param>
/// <param name="Metadata">What to stamp into a PDF besides its pages; null means the writer's defaults.</param>
/// <param name="ToServer">Save into <c>output.directory</c> instead of offering a download. Refused when it is not configured.</param>
[TranspilationSource]
public sealed record ExportRequest(ExportFormat Format = ExportFormat.Pdf, string? Name = null, DocumentMetadataDto? Metadata = null, bool ToServer = false);

/// <summary>
/// What to stamp into a PDF besides its pages, as the browser keeps it in its settings. The
/// title is not here: it is the document's name, which the export already knows.
/// </summary>
[TranspilationSource]
public sealed record DocumentMetadataDto(string? Author = null, string? Subject = null, string? Keywords = null);

/// <param name="PageCount">Pages across every produced document.</param>
/// <param name="Documents">One entry per document in the session, in order.</param>
[TranspilationSource]
public sealed record ExportResult(int PageCount, ExportedDocumentDto[] Documents);

/// <param name="PageCount">Pages in this document.</param>
/// <param name="DownloadPath">One-shot relative URL the browser fetches the file from; empty when saved to the server.</param>
/// <param name="SavedTo">Where on the server the file was saved, when it was.</param>
[TranspilationSource]
public sealed record ExportedDocumentDto(int PageCount, string DownloadPath, string? SavedTo = null);
