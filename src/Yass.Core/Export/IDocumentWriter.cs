using Yass.Core.Config;
using Yass.Core.Hub;

namespace Yass.Core.Export;

public interface IDocumentWriter
{
    /// <summary>The writer does not close <c>destination</c>.</summary>
    Task WritePdfAsync(
        IReadOnlyList<ExportPage> pages,
        PdfConfig pdf,
        DocumentMetadata metadata,
        Stream destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken);

    Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken);
}
