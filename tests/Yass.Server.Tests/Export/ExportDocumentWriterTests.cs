using Microsoft.Extensions.Logging.Abstractions;
using Yass.App.Export;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.Server.Tests.Export;

public sealed class ExportDocumentWriterTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task An_interrupted_write_removes_the_partial_file_and_preserves_existing_documents(bool toServer, bool cancel)
    {
        string directory = Path.Combine(Path.GetTempPath(), "yass-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "letter.pdf");
        await File.WriteAllTextAsync(existing, "existing document");
        try
        {
            var writer = new ExportDocumentWriter(new InterruptedWriter(cancel), new DownloadTokens(),
                new DownloadPaths(), NullLogger<ExportDocumentWriter>.Instance);
            var document = new ExportDocument([], DocumentMetadata.None, "letter.pdf");

            Exception? error = await Record.ExceptionAsync(() => writer.WriteAsync("session", document,
                ExportFormat.Pdf, new PdfConfig(), directory, toServer, new Progress<int>(), CancellationToken.None));

            if (cancel)
            {
                Assert.IsType<OperationCanceledException>(error);
            }
            else
            {
                Assert.IsType<IOException>(error);
            }

            Assert.Equal([existing], Directory.GetFiles(directory));
            Assert.Equal("existing document", await File.ReadAllTextAsync(existing));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class InterruptedWriter(bool cancel) : IDocumentWriter
    {
        public Task WritePdfAsync(IReadOnlyList<ExportPage> pages, PdfConfig pdf, DocumentMetadata metadata,
            Stream destination, IProgress<int>? progress, CancellationToken cancellationToken) =>
            InterruptAsync(destination, cancellationToken);

        public Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality,
            Stream destination, CancellationToken cancellationToken) => InterruptAsync(destination, cancellationToken);

        private async Task InterruptAsync(Stream destination, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
            if (cancel)
            {
                throw new OperationCanceledException();
            }

            throw new IOException("Document writer failed after writing bytes.");
        }
    }
}
