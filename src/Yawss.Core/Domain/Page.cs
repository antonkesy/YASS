namespace Yawss.Core.Domain;

/// <summary>
/// One scanned page. <see cref="SourceFile"/> is never modified; every edit is recorded on
/// this record and derived images are a pure function of (source, edits), keyed by <see cref="Hash"/>.
/// </summary>
/// <param name="Index">Zero-based position within the session, in scan order.</param>
/// <param name="SourceFile">Path of the original PNG, relative to the session directory.</param>
/// <param name="Rotation">Rotation to apply when rendering or exporting.</param>
/// <param name="ColorOverride">Per-page colour mode override; <c>null</c> keeps the scan's colour mode.</param>
/// <param name="Deleted">Excluded from export. Reversible.</param>
/// <param name="IsBlank">Flagged by blank-page detection at scan time; such pages start out deleted.</param>
/// <param name="SourceHash">Hash of the source file bytes, fixed at scan time.</param>
/// <param name="Hash">Content hash of (source, edits); changes whenever a derived image would change.</param>
public sealed record Page(
    int Index,
    string SourceFile,
    Rotation Rotation,
    ColorMode? ColorOverride,
    bool Deleted,
    bool IsBlank,
    string SourceHash,
    string Hash);
