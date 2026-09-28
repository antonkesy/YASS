using Microsoft.Extensions.Logging;
using NAPS2.Images;
using NAPS2.Images.ImageSharp;
using NAPS2.ImportExport;
using NAPS2.Pdf;
using NAPS2.Scan;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.Export;

/// <summary>Assembles a PDF with NAPS2's <see cref="PdfExporter"/> from pages pre-rendered by <see cref="PageRenderer"/>.</summary>
public sealed class Naps2PdfWriter : IDocumentWriter, IDisposable
{
    private readonly ScanningContext _context;
    private readonly ILogger<Naps2PdfWriter> _logger;

    public Naps2PdfWriter(ILogger<Naps2PdfWriter> logger)
    {
        _logger = logger;
        _context = new ScanningContext(new ImageSharpImageContext()) { Logger = logger };
    }

    public async Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(quality);
        await PageRenderer.RenderToStreamAsync(page, format, quality, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task WritePdfAsync(IReadOnlyList<ExportPage> pages, PdfConfig pdf, Stream destination, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(destination);
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        string work = Path.Combine(Path.GetTempPath(), "yass-export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var images = new List<ProcessedImage>(pages.Count);
        try
        {
            var importer = new ImageImporter(_context);
            for (int i = 0; i < pages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string rendered = await PageRenderer.RenderToFileAsync(pages[i], pdf, work, cancellationToken).ConfigureAwait(false);
                await foreach (ProcessedImage image in importer.Import(rendered).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    images.Add(image);
                }

                // Rendering is the expensive part; leave the last 10 % for NAPS2 writing the file.
                progress?.Report((i + 1) * 90 / pages.Count);
            }

            var exporter = new PdfExporter(_context);
            var parameters = new PdfExportParams { Metadata = new PdfMetadata { Creator = "YASS", Author = "YASS" } };
            bool ok = await exporter.Export(destination, images, parameters).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                throw new IOException("NAPS2 PdfExporter reported failure.");
            }

            progress?.Report(100);
            _logger.LogDebug("Assembled PDF with {Pages} page(s)", pages.Count);
        }
        finally
        {
            foreach (ProcessedImage image in images)
            {
                image.Dispose();
            }

            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // Temp cleanup is best effort.
            }
        }
    }

    public void Dispose() => _context.Dispose();
}
