using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.App.Export;

/// <summary>Writes and publishes one complete document, cleaning up any unpublished file on failure.</summary>
public sealed class ExportDocumentWriter(
    IDocumentWriter writer,
    DownloadTokens tokens,
    IDownloadPaths downloadPaths,
    ILogger<ExportDocumentWriter> logger)
{
    /// <summary>Hidden until the completed document is renamed into the server folder.</summary>
    public const string TempPrefix = "._yass-";

    internal async Task<ExportedDocumentDto> WriteAsync(
        string sessionId, ExportDocument document, ExportFormat format, PdfConfig pdf,
        string directory, bool toServer, IProgress<int> progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        string extension = Path.GetExtension(document.FileName);
        string path = Path.Combine(directory, toServer
            ? $"{TempPrefix}{Guid.NewGuid():N}.tmp"
            : $"{Guid.NewGuid():N}{extension}");
        bool published = false;
        try
        {
            FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
            await using (file.ConfigureAwait(false))
            {
                if (format == ExportFormat.Pdf)
                {
                    await writer.WritePdfAsync(document.Pages, pdf, document.Metadata, file, progress, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // A browser download is one file, so an image download is the document's first page.
                    await writer.WriteImageAsync(document.Pages[0], format, pdf, file, cancellationToken).ConfigureAwait(false);
                }

                if (toServer)
                {
                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA1849 // fsync has no async counterpart; the rename must not outrun the data.
                    file.Flush(flushToDisk: true);
#pragma warning restore CA1849
                }
            }

            ExportedDocumentDto result;
            if (toServer)
            {
                string saved = FileNames.Unique(directory, document.FileName);
                File.Move(path, saved);
                logger.LogInformation("Saved {File}", saved);
                result = new ExportedDocumentDto(document.Pages.Length, "", saved);
            }
            else
            {
                string token = tokens.Issue(sessionId, path);
                result = new ExportedDocumentDto(document.Pages.Length, downloadPaths.Download(sessionId, token, extension));
            }

            published = true;
            return result;
        }
        finally
        {
            if (!published && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
