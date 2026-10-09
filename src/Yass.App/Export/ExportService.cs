using System.Globalization;
using Yass.App.Api;
using Yass.App.Config;
using Yass.App.Sessions;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;
using Yass.Export;

namespace Yass.App.Export;

/// <summary>
/// Exports a session's active pages into the session's <c>export/</c> folder and hands out a
/// one-shot download URL per document (spelt by the host's <see cref="IDownloadPaths"/>). The server never writes a document anywhere else:
/// where it ends up is the browser's choice. Progress is pushed as <c>ExportProgress</c>.
/// </summary>
public sealed class ExportService(
    SessionStore store,
    SessionMapper mapper,
    IDocumentWriter writer,
    DownloadTokens tokens,
    ConfigStore config,
    TimeProvider time,
    IClientNotifier hub,
    IDownloadPaths downloadPaths,
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
    /// <summary>Prefix of a file still being written into the server folder, renamed once complete.</summary>
    public const string TempPrefix = "._yass-";

    public async Task<ExportResult> ExportAsync(string sessionId, ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Checked before the session turns Exporting, so a refusal leaves nothing to undo.
        OutputConfig output = config.Current.Output;
        if (request.ToServer && !output.SavesToServer())
        {
            throw new InvalidOperationException("Saving to the server is off: output.directory is not set in yass.yaml.");
        }

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
        await hub.All.SessionChanged(mapper.ToDto(session)).ConfigureAwait(false);

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
            _ = hub.All.ExportProgress(sessionId, Math.Clamp(overall, 0, 100));
        });
        PdfConfig pdf = output.Pdf;

        string? pending = null;
        try
        {
            string dir = request.ToServer ? output.Directory : SessionPaths.Export(store.Root, sessionId);
            Directory.CreateDirectory(dir);
            var produced = new List<ExportedDocumentDto>();
            for (int d = 0; d < documentPages.Length; d++)
            {
                ExportPage[] document = documentPages[d];
                string? token = null;
                string path;
                if (request.ToServer)
                {
                    // Written under a hidden temporary name and renamed once complete, so whatever
                    // watches the folder — paperless-ngx, say — never picks up half a document.
                    path = Path.Combine(dir, $"{TempPrefix}{Guid.NewGuid():N}.tmp");
                    pending = path;
                }
                else
                {
                    token = tokens.Issue(sessionId, Path.Combine(dir, $"{Guid.NewGuid():N}{extension}"));
                    tokens.TryPeekPath(token, out path);
                }

                FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
                await using (file.ConfigureAwait(false))
                {
                    if (request.Format == ExportFormat.Pdf)
                    {
                        DocumentMetadata metadata = MetadataFor(request, NameFor(request.Name, session, facts, documentPages.Length, d));
                        await writer.WritePdfAsync(document, pdf, metadata, file, progress, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // A browser download is one file, so an image download is the first
                        // page of the document rather than a silent partial archive.
                        await writer.WriteImageAsync(document[0], request.Format, pdf, file, cancellationToken).ConfigureAwait(false);
                    }

                    if (request.ToServer)
                    {
                        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA1849 // fsync has no async counterpart; the rename must not outrun the data.
                        file.Flush(flushToDisk: true);
#pragma warning restore CA1849
                    }
                }

                if (request.ToServer)
                {
                    string stem = SafeFileName(NameFor(request.Name, session, facts, documentPages.Length, d)
                        ?? Fallback(session, documentPages.Length, d));
                    string saved = UniquePath(dir, stem + extension);
                    File.Move(path, saved);
                    pending = null;
                    logger.LogInformation("Saved {File}", saved);
                    produced.Add(new ExportedDocumentDto(document.Length, "", saved));
                }
                else
                {
                    produced.Add(new ExportedDocumentDto(document.Length, downloadPaths.Download(sessionId, token!, extension)));
                }

                finished++;
            }

            var result = new ExportResult(totalPages, [.. produced]);
            var recorded = request.ToServer
                ? new SessionEvent(SessionEventKind.Exported, time.GetUtcNow(), produced[0].SavedTo)
                : new SessionEvent(SessionEventKind.Downloaded, time.GetUtcNow());
            logger.LogInformation(
                "Session {Session} exported as {Documents} document(s), {Pages} page(s) total",
                sessionId,
                produced.Count,
                totalPages);

            await hub.All.ExportProgress(sessionId, 100).ConfigureAwait(false);
            Session done = store.Update(sessionId, s => (s with { State = SessionState.Done }).With(recorded));
            await hub.All.SessionChanged(mapper.ToDto(done)).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Export of {Session} failed", sessionId);
            Session failed = store.Update(sessionId, s => s with { State = SessionState.Review, Error = ex.Message });
            await hub.All.SessionChanged(mapper.ToDto(failed)).ConfigureAwait(false);
            throw;
        }
        finally
        {
            // A save into the server folder that did not finish leaves no half-written file behind.
            if (pending is not null && File.Exists(pending))
            {
                File.Delete(pending);
            }
        }
    }

    /// <summary>A name for an unnamed scan, from when it was scanned — what the browser would suggest too.</summary>
    private static string Fallback(Session session, int documentCount, int index)
    {
        string stem = "scan-" + session.CreatedAt.ToString("yyyy-MM-dd-HH-mm", CultureInfo.InvariantCulture);
        return documentCount == 1 ? stem : $"{stem}-{(index + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// A name made safe to put in the folder: no path separators or characters the file system
    /// refuses, so a name can never climb out of <c>output.directory</c>.
    /// </summary>
    internal static string SafeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(name.Select(c => invalid.Contains(c) || c == '\\' ? '-' : c).ToArray());
        safe = safe.Trim().Trim('.');
        return safe.Length == 0 ? "scan" : safe;
    }

    /// <summary>The name, or the name with -2, -3… before the extension: never over an existing file.</summary>
    internal static string UniquePath(string directory, string fileName)
    {
        string candidate = Path.Combine(directory, fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{stem}-{n.ToString(CultureInfo.InvariantCulture)}{extension}");
        }

        return candidate;
    }
}
