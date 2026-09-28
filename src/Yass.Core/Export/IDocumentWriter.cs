using Yass.Core.Config;
using Yass.Core.Hub;

namespace Yass.Core.Export;

/// <summary>Turns the pages of a session into files.</summary>
public interface IDocumentWriter
{
    /// <param name="pages">Pages in output order; deleted pages are already excluded.</param>
    /// <param name="pdf">Compression settings.</param>
    /// <param name="destination">Receives the PDF bytes. Not closed by the writer.</param>
    /// <param name="progress">0..100, reported at least once per page.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    Task WritePdfAsync(
        IReadOnlyList<ExportPage> pages,
        PdfConfig pdf,
        Stream destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken);

    /// <summary>Writes one page as a standalone image. Used by the JPG and PNG formats, which produce a file per page.</summary>
    Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken);
}
