using Yass.Core.Config;

namespace Yass.Core.Export;

/// <summary>Assembles the pages of a session into a PDF.</summary>
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
}
