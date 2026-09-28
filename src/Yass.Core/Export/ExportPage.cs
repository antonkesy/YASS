using Yass.Core.Domain;

namespace Yass.Core.Export;

/// <summary>A page to export: the untouched source file plus the edits to apply while exporting.</summary>
/// <param name="SourcePath">Absolute path of the original PNG.</param>
/// <param name="Rotation">Rotation to apply.</param>
/// <param name="Filter">Per-page treatment to apply.</param>
/// <param name="ScanColor">Colour mode the page was scanned in, which <see cref="PageFilter.Original"/> keeps.</param>
/// <param name="Crop">Trim to apply before rotating, or <c>null</c>.</param>
/// <param name="Dpi">Resolution the page was scanned at; used for the PDF page size when the file carries none.</param>
public sealed record ExportPage(string SourcePath, Rotation Rotation, PageFilter Filter, ColorMode ScanColor, CropBox? Crop, int Dpi)
{
    /// <summary>The colour mode this page actually renders in.</summary>
    public ColorMode ColorMode => Filter.ColorFor(ScanColor);
}
