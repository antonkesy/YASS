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
/// Exports a session's active pages. <see cref="ExportMode.Save"/> writes into the consume directory through
/// <see cref="IDocumentStore"/>; <see cref="ExportMode.Download"/> writes into the session's <c>export/</c>
/// folder and hands out a one-shot URL. Progress is pushed as <c>ExportProgress</c>.
/// </summary>
public sealed class ExportService(
    SessionStore store,
    SessionMapper mapper,
    IDocumentWriter writer,
    IDocumentStore documents,
    DownloadTokens tokens,
    ConfigStore config,
    TimeProvider time,
    IHubContext<YassHub, IYassClient> hub,
    ILogger<ExportService> logger)
{
    /// <summary>
    /// Checks a user-supplied target directory. The service has no authentication, so this is
    /// the only gate between a LAN client and an arbitrary write path: the directory must be
    /// absolute, inside <c>output.allowed_roots</c>, existing and writable, and the error says
    /// which of those failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">The directory is unusable or out of bounds.</exception>
    private string? ResolveDirectory(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        string path = requested.Trim();
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"Export folder must be an absolute path; got \"{path}\".");
        }

        OutputConfig output = config.Current.Output;
        IReadOnlyList<string> roots = ExportRoots.Effective(output.AllowedRoots, output.Directory);
        if (!ExportRoots.Allows(roots, path))
        {
            throw new InvalidOperationException(
                $"Export folder \"{path}\" is outside the allowed roots ({string.Join(", ", roots)}). " +
                "Add it to output.allowed_roots to permit it.");
        }

        if (!Directory.Exists(path))
        {
            throw new InvalidOperationException($"Export folder \"{path}\" does not exist.");
        }

        try
        {
            string probe = Path.Combine(path, "._yass-write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Export folder \"{path}\" is not writable.");
        }

        return path;
    }

    /// <summary>The file name for one document, or <c>null</c> to fall back to the configured template.</summary>
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

        // Checked before the session moves to Exporting: a refused folder must leave the
        // session exactly as it was, not stuck mid-export with no way back.
        string? directory = ResolveDirectory(request.Directory);

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
                .Select(p => new ExportPage(SessionPaths.Source(store.Root, session, p), p.Rotation, p.Filter, session.OptionsUsed.Color, p.Crop, session.OptionsUsed.Dpi))
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
            ExportResult result;
            SessionEvent recorded;
            if (request.Mode == ExportMode.Download)
            {
                if (!config.Current.Output.AllowDownload)
                {
                    throw new InvalidOperationException("Downloads are disabled (output.allow_download).");
                }

                string dir = SessionPaths.Export(store.Root, sessionId);
                Directory.CreateDirectory(dir);
                var produced = new List<ExportedDocumentDto>();
                foreach (ExportPage[] document in documentPages)
                {
                    string token = tokens.Issue(sessionId, Path.Combine(dir, $"{Guid.NewGuid():N}{extension}"));
                    tokens.TryPeekPath(token, out string path);
                    await using (FileStream output = File.Create(path))
                    {
                        if (request.Format == ExportFormat.Pdf)
                        {
                            await writer.WritePdfAsync(document, pdf, output, progress, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // A browser download is one file, so an image download is the first
                            // page of the document rather than a silent partial archive.
                            await writer.WriteImageAsync(document[0], request.Format, pdf, output, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    produced.Add(new ExportedDocumentDto(document.Length, null, $"/export/{sessionId}/{token}{extension}"));
                    finished++;
                }

                result = new ExportResult(ExportMode.Download, totalPages, [.. produced]);
                recorded = new SessionEvent(SessionEventKind.Downloaded, time.GetUtcNow());
            }
            else
            {
                var written = new List<ExportedDocumentDto>();
                for (int d = 0; d < documentPages.Length; d++)
                {
                    ExportPage[] document = documentPages[d];
                    string? baseName = NameFor(request.Name, session, facts, documentPages.Length, d);

                    if (request.Format == ExportFormat.Pdf)
                    {
                        string saved = await documents.SaveAsync(
                            (stream, ct) => writer.WritePdfAsync(document, pdf, stream, progress, ct),
                            new DocumentTarget(session.CreatedAt, baseName, directory, extension),
                            cancellationToken).ConfigureAwait(false);
                        written.Add(new ExportedDocumentDto(document.Length, saved, null));
                    }
                    else
                    {
                        // Images are one file per page, so the document's pages are numbered.
                        string? firstPath = null;
                        for (int p = 0; p < document.Length; p++)
                        {
                            ExportPage page = document[p];
                            string pageName = document.Length == 1
                                ? baseName ?? ""
                                : $"{baseName}-{(p + 1).ToString("D2", CultureInfo.InvariantCulture)}";
                            string saved = await documents.SaveAsync(
                                (stream, ct) => writer.WriteImageAsync(page, request.Format, pdf, stream, ct),
                                new DocumentTarget(session.CreatedAt, string.IsNullOrEmpty(pageName) ? null : pageName, directory, extension),
                                cancellationToken).ConfigureAwait(false);
                            firstPath ??= saved;
                            ((IProgress<int>)progress).Report((p + 1) * 100 / document.Length);
                        }

                        written.Add(new ExportedDocumentDto(document.Length, firstPath, null));
                    }

                    finished++;
                }

                result = new ExportResult(ExportMode.Save, totalPages, [.. written]);
                recorded = new SessionEvent(SessionEventKind.Exported, time.GetUtcNow(), written[0].SavedPath);
                logger.LogInformation(
                    "Session {Session} saved as {Documents} document(s), {Pages} page(s) total",
                    sessionId,
                    written.Count,
                    totalPages);
            }

            await hub.Clients.All.ExportProgress(sessionId, 100).ConfigureAwait(false);

            if (result.Mode == ExportMode.Save && config.Current.Output.OnSuccess == OnSuccessAction.DeleteSession)
            {
                store.Delete(sessionId);
                await hub.Clients.All.SessionChanged(mapper.ToDto((session with { State = SessionState.Done, Pages = [], Error = "deleted" }).With(recorded))).ConfigureAwait(false);
            }
            else
            {
                Session done = store.Update(sessionId, s => (s with { State = SessionState.Done }).With(recorded));
                await hub.Clients.All.SessionChanged(mapper.ToDto(done)).ConfigureAwait(false);
            }

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
