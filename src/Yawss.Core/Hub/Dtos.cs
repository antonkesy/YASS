using Tapper;
using Yawss.Core.Domain;
using Yawss.Core.Scanning;

namespace Yawss.Core.Hub;

/// <summary>Wire shape of a session. Dates are ISO-8601 strings so they survive MessagePack and TypeScript unchanged.</summary>
[TranspilationSource]
public sealed record SessionDto(
    string Id,
    string CreatedAt,
    SessionState State,
    PageDto[] Pages,
    ScanOptionsDto OptionsUsed,
    string? Error);

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
