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
    ColorMode? ColorOverride,
    bool Deleted,
    bool IsBlank,
    string Hash,
    bool ThumbnailReady);

/// <summary>
/// Scan options on the wire. Every field is optional: as a <c>StartScan</c> argument only the keys in
/// <c>scanner.ui_editable</c> are honoured; as a <c>GetEffectiveOptions</c> result every field is filled in and
/// <see cref="Editable"/> lists the keys the UI may change and <see cref="AllowDownload"/> mirrors <c>output.allow_download</c>.
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
    string[]? Editable = null,
    bool? AllowDownload = null);

[TranspilationSource]
public sealed record ScannerStatusDto(ScannerState State, string? DeviceName, string? Message, string CheckedAt);

[TranspilationSource]
public enum ExportMode
{
    /// <summary>Write the PDF into <c>output.directory</c>.</summary>
    Save = 0,

    /// <summary>Produce a one-shot download URL for the browser.</summary>
    Download,
}

[TranspilationSource]
public sealed record ExportRequest(ExportMode Mode);

/// <param name="Mode">Echo of the request.</param>
/// <param name="PageCount">Pages in the produced PDF.</param>
/// <param name="SavedPath">Final path when <see cref="ExportMode.Save"/>.</param>
/// <param name="DownloadPath">Relative URL path when <see cref="ExportMode.Download"/>; valid for one GET.</param>
[TranspilationSource]
public sealed record ExportResult(ExportMode Mode, int PageCount, string? SavedPath, string? DownloadPath);
