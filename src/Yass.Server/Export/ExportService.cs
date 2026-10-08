using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;
using Yass.Export;
using Yass.Server.Config;
using Yass.Server.Hub;
using Yass.Server.Sessions;

namespace Yass.Server.Export;

/// <summary>
/// Exports a session's active pages into the session's <c>export/</c> folder and hands out a
/// one-shot download URL per document. The server never writes a document anywhere else:
/// where it ends up is the browser's choice. Progress is pushed as <c>ExportProgress</c>.
/// </summary>
public sealed class ExportService(
    SessionStore store,
    SessionMapper mapper,
    IDocumentWriter writer,
    DownloadTokens tokens,
    ConfigStore config,
    TimeProvider time,
    IHubContext<YassHub, IYassClient> hub,
    ILogger<ExportService> logger)
{
    /// <summary>The PDF's own properties: its name as the title, and whatever the browser's settings say for the rest.</summary>
    private static DocumentMetadata MetadataFor(ExportRequest request, string? name) =>
        new(name, request.Metadata?.Author, request.Metadata?.Subject, request.Metadata?.Keywords);

    /// <summary>The file name for one document, or <c>null</c> when neither the request nor the session names it.</summary>
    private static string? NameFor(string? requested, Session session, TemplateFacts facts, int documentCount, int index)
    {
        string? stem = !string.IsNullOrWhiteSpace(requested) ? requested!.Trim()
            : !string.IsNullOrWhiteSpace(session.Name) ? session.Name!.Trim()
            : null;

        if (stem is null)
        {
            return null;
        }

        stem = FilenameTemplate.Expand(stem, session.CreatedAt, session.Id, facts, extension: "");
        return documentCount == 1 ? stem : $"{stem}-{(index + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <exception cref="InvalidOperationException">Nothing to export or the session is busy.</exception>
    public async Task<ExportResult> ExportAsync(string sessionId, ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Session session = store.Update(sessionId, s =>
        {
            if (s.State is not (SessionState.Review or SessionState.Done))
            {
                throw new InvalidOperationException($"Session is {s.State}; export is only possible in Review.");
            }

            if (!s.ActivePages.Any())
            {
                throw new InvalidOperationException("No pages to export; every page is deleted.");
            }

            return s with { State = SessionState.Exporting, Error = null };
        });
        await hub.Clients.All.SessionChanged(mapper.ToDto(session)).ConfigureAwait(false);

        string extension = request.Format switch
        {
            ExportFormat.Jpg => ".jpg",
            ExportFormat.Png => ".png",
            _ => ".pdf",
        };
        var facts = new TemplateFacts(
            Preset: string.IsNullOrWhiteSpace(session.Name) ? "Scan" : session.Name!,
            Counter: 1,
            Dpi: session.OptionsUsed.Dpi,
            Color: session.OptionsUsed.Color.ToString());

        ExportPage[][] documentPages = session.Documents
            .Select(doc => doc
                .Select(p => new ExportPage(SessionPaths.Source(store.Root, session, p), p.Rotation, p.Filter, session.OptionsUsed.Color, p.Crop, session.OptionsUsed.Dpi, p.Adjust, p.Cleanup, p.Skew))
                .ToArray())
            .ToArray();
        int totalPages = documentPages.Sum(d => d.Length);

        // Progress is reported across the whole export, so several documents still fill one bar.
        int finished = 0;
        var progress = new Progress<int>(percent =>
        {
            int overall = (int)((finished + percent / 100.0) / documentPages.Length * 100);
            _ = hub.Clients.All.ExportProgress(sessionId, Math.Clamp(overall, 0, 100));
        });
        PdfConfig pdf = config.Current.Output.Pdf;

        try
        {
            string dir = SessionPaths.Export(store.Root, sessionId);
            Directory.CreateDirectory(dir);
            var produced = new List<ExportedDocumentDto>();
            for (int d = 0; d < documentPages.Length; d++)
            {
                ExportPage[] document = documentPages[d];
                string token = tokens.Issue(sessionId, Path.Combine(dir, $"{Guid.NewGuid():N}{extension}"));
                tokens.TryPeekPath(token, out string path);
                await using (FileStream output = File.Create(path))
                {
                    if (request.Format == ExportFormat.Pdf)
                    {
                        DocumentMetadata metadata = MetadataFor(request, NameFor(request.Name, session, facts, documentPages.Length, d));
                        await writer.WritePdfAsync(document, pdf, metadata, output, progress, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // A browser download is one file, so an image download is the first
                        // page of the document rather than a silent partial archive.
                        await writer.WriteImageAsync(document[0], request.Format, pdf, output, cancellationToken).ConfigureAwait(false);
                    }
                }

                produced.Add(new ExportedDocumentDto(document.Length, $"/export/{sessionId}/{token}{extension}"));
                finished++;
            }

            var result = new ExportResult(totalPages, [.. produced]);
            var recorded = new SessionEvent(SessionEventKind.Downloaded, time.GetUtcNow());
            logger.LogInformation(
                "Session {Session} exported as {Documents} document(s), {Pages} page(s) total",
                sessionId,
                produced.Count,
                totalPages);

            await hub.Clients.All.ExportProgress(sessionId, 100).ConfigureAwait(false);
            Session done = store.Update(sessionId, s => (s with { State = SessionState.Done }).With(recorded));
            await hub.Clients.All.SessionChanged(mapper.ToDto(done)).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Export of {Session} failed", sessionId);
            Session failed = store.Update(sessionId, s => s with { State = SessionState.Review, Error = ex.Message });
            await hub.Clients.All.SessionChanged(mapper.ToDto(failed)).ConfigureAwait(false);
            throw;
        }
    }
}
