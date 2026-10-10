namespace Yass.Core.Domain;

/// <summary>
/// <see cref="SourceFile"/> is never modified; derived images are a pure function of (source, edits), keyed by <see cref="Hash"/>.
/// </summary>
public sealed record Page(
    int Index,
    string SourceFile,
    Rotation Rotation,
    PageFilter Filter,
    CropBox? Crop,
    bool StartsDocument,
    bool Deleted,
    bool IsBlank,
    string SourceHash,
    string Hash,
    PageAdjust? Adjust = null,
    PageCleanup Cleanup = PageCleanup.None,
    double Skew = 0);
