using Tapper;
using Yass.Core.Domain;
using Yass.Core.Scanning;

namespace Yass.Core.Hub;

/// <summary>Dates are ISO-8601 strings so they survive MessagePack and TypeScript unchanged.</summary>
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
/// Every field optional: as a <c>StartScan</c> argument each one sent overrides <c>scanner.defaults</c>;
/// as a <c>GetEffectiveOptions</c> result every field is filled in.
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

[TranspilationSource]
public sealed record ScannerDeviceDto(string Id, string Name, string? Driver = null, string? Connection = null);

[TranspilationSource]
public enum ExportFormat
{
    Pdf = 0,
    Jpg,
    Png,
}

/// <summary>Empty <c>Name</c> falls back to the session's name. <c>ToServer</c> is refused when <c>output.directory</c> is not set.</summary>
[TranspilationSource]
public sealed record ExportRequest(ExportFormat Format = ExportFormat.Pdf, string? Name = null, DocumentMetadataDto? Metadata = null, bool ToServer = false);

/// <summary>No title: it is the document's name, which the export already knows.</summary>
[TranspilationSource]
public sealed record DocumentMetadataDto(string? Author = null, string? Subject = null, string? Keywords = null);

[TranspilationSource]
public sealed record ExportResult(int PageCount, ExportedDocumentDto[] Documents);

/// <summary><c>DownloadPath</c> is a one-shot relative URL, empty when saved to the server.</summary>
[TranspilationSource]
public sealed record ExportedDocumentDto(int PageCount, string DownloadPath, string? SavedTo = null);
