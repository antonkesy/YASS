using Yass.Core.Domain;

namespace Yass.Core.Export;

/// <summary>A page to export: the untouched source file plus the edits to apply while exporting.</summary>
/// <param name="SourcePath">Absolute path of the original PNG.</param>
/// <param name="Spec">
/// What the pipeline renders: the same record the page's hash and its previews are made from, so
/// an export cannot miss an edit they show.
/// </param>
/// <param name="Dpi">Resolution the page was scanned at; used for the PDF page size when the file carries none.</param>
public sealed record ExportPage(string SourcePath, RenderSpec Spec, int Dpi)
{
    /// <summary>The colour mode this page actually renders in.</summary>
    public ColorMode ColorMode => Spec.ColorMode;
}
