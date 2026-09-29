using Tapper;
using Yass.Core.Domain;
using Yass.Core.Naming;
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
    bool ThumbnailReady);


/// <summary>
/// Scan options on the wire. Every field is optional: as a <c>StartScan</c> argument only the keys in
/// <c>scanner.ui_editable</c> are honoured; as a <c>GetEffectiveOptions</c> result every field is filled in and
/// <see cref="Editable"/> lists the keys the UI may change, <see cref="AllowDownload"/> mirrors
/// <c>output.allow_download</c>, <see cref="ExportRoots"/> mirrors the directories the export
/// folder may sit under so the settings sheet can offer them instead of only rejecting a typed path,
/// and <see cref="CanReadPageText"/> says whether asking for a name is worth the round trip at all.
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
    bool? AllowDownload = null,
    string[]? ExportRoots = null,
    bool? CanReadPageText = null,
    int? PageTextPages = null);

/// <param name="Suggestions">Best first.</param>
/// <param name="CanRead">Whether page text could be read at all, which decides what the UI can offer.</param>
/// <param name="Local">Whether the text stayed on the scanner server, which the UI says out loud.</param>
/// <param name="Text">What was read off the pages, for a browser that wants to ask its own model.</param>
/// <param name="Error">Why the server's own provider produced nothing, in words fit to show someone.</param>
[TranspilationSource]
public sealed record NameSuggestionsDto(
    NameSuggestion[] Suggestions,
    bool CanRead,
    bool Local,
    string? Text = null,
    string? Error = null);

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

/// <summary>File type a session is exported as. Images write one file per page.</summary>
[TranspilationSource]
public enum ExportFormat
{
    Pdf = 0,
    Jpg,
    Png,
}

/// <param name="Mode">Save to a folder, or hand the browser a download.</param>
/// <param name="Format">File type to produce.</param>
/// <param name="Name">Base file name, without extension; empty falls back to the configured template.</param>
/// <param name="Directory">Where to write, for <see cref="ExportMode.Save"/>; empty means <c>output.directory</c>.</param>
[TranspilationSource]
public sealed record ExportRequest(ExportMode Mode, ExportFormat Format = ExportFormat.Pdf, string? Name = null, string? Directory = null);

/// <param name="Mode">Echo of the request.</param>
/// <param name="PageCount">Pages across every produced document.</param>
/// <param name="Documents">One entry per document in the session, in order.</param>
[TranspilationSource]
public sealed record ExportResult(ExportMode Mode, int PageCount, ExportedDocumentDto[] Documents);

/// <param name="PageCount">Pages in this document.</param>
/// <param name="SavedPath">Where it was written, for <see cref="ExportMode.Save"/>.</param>
/// <param name="DownloadPath">One-shot relative URL, for <see cref="ExportMode.Download"/>.</param>
[TranspilationSource]
public sealed record ExportedDocumentDto(int PageCount, string? SavedPath, string? DownloadPath);
