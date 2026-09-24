namespace Yawss.Core.Domain;

/// <summary>A scan session: a directory of source pages plus the edits applied to them.</summary>
/// <param name="Id">Opaque, URL-safe identifier; also the session directory name.</param>
/// <param name="CreatedAt">When the scan was started.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="Pages">Pages in display order.</param>
/// <param name="OptionsUsed">Effective options the scan was started with.</param>
public sealed record Session(
    string Id,
    DateTimeOffset CreatedAt,
    SessionState State,
    IReadOnlyList<Page> Pages,
    ScanOptions OptionsUsed);
