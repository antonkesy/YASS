using Yawss.Core.Domain;

namespace Yawss.Core.Export;

/// <summary>A page to export: the untouched source file plus the edits to apply while exporting.</summary>
/// <param name="SourcePath">Absolute path of the original PNG.</param>
/// <param name="Rotation">Rotation to apply.</param>
/// <param name="ColorMode">Effective colour mode (page override or the session's scan colour mode).</param>
public sealed record ExportPage(string SourcePath, Rotation Rotation, ColorMode ColorMode);
