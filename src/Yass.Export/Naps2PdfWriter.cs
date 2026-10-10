using Microsoft.Extensions.Logging;
using NAPS2.Images;
using NAPS2.Images.ImageSharp;
using NAPS2.ImportExport;
using NAPS2.Pdf;
using NAPS2.Scan;
using NAPS2.Util;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.Export;

public sealed class Naps2PdfWriter(ILogger<Naps2PdfWriter> logger) : IDocumentWriter
{
    public async Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(quality);
        await PageRenderer.RenderToStreamAsync(page, format, quality, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task WritePdfAsync(IReadOnlyList<ExportPage> pages, PdfConfig pdf, DocumentMetadata metadata, Stream destination, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(destination);
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        string work = Path.Combine(Path.GetTempPath(), "yass-export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var images = new List<ProcessedImage>(pages.Count);
        // File-backed so pages are not all decoded in memory at once (SPEC §9), and an untransformed
        // JPEG goes into the PDF verbatim.
        var context = new ScanningContext(new ImageSharpImageContext()) { Logger = logger, FileStorageManager = new FileStorageManager(work) };
        try
        {
            var importer = new ImageImporter(context);
            for (int i = 0; i < pages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string rendered = await PageRenderer.RenderToFileAsync(pages[i], pdf, work, cancellationToken).ConfigureAwait(false);
                await foreach (ProcessedImage image in importer.Import(rendered).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    images.Add(image);
                }

                progress?.Report((i + 1) * 90 / pages.Count);
            }

            var exporter = new PdfExporter(context);
            // Creator stays ours; the rest is exactly what the request says, empty stays empty.
            var parameters = new PdfExportParams
            {
                Metadata = new PdfMetadata
                {
                    Creator = "YASS",
                    Title = metadata.Title?.Trim() ?? "",
                    Author = metadata.Author?.Trim() ?? "",
                    Subject = metadata.Subject?.Trim() ?? "",
                    Keywords = metadata.Keywords?.Trim() ?? "",
                },
            };
            // Cancelled through NAPS2's handler so it stops before the files it reads are disposed below.
            var handler = new ProgressHandler((current, max) => progress?.Report(90 + (current * 10 / Math.Max(max, 1))), cancellationToken);
            bool ok = await exporter.Export(destination, images, parameters, progress: handler).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ok)
            {
                throw new IOException("NAPS2 PdfExporter reported failure.");
            }

            progress?.Report(100);
            logger.LogDebug("Assembled PDF with {Pages} page(s)", pages.Count);
        }
        finally
        {
            foreach (ProcessedImage image in images)
            {
                image.Dispose();
            }

            context.Dispose();
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort; must not hide why the export failed.
            }
        }
    }
}
