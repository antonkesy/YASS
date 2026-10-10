using Yass.Core.Domain;

namespace Yass.Core.Export;

public sealed record ExportPage(string SourcePath, RenderSpec Spec, int Dpi)
{
    public ColorMode ColorMode => Spec.ColorMode;
}
