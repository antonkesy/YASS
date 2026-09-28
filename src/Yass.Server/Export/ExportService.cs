using Microsoft.AspNetCore.SignalR;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;
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

        var pages = session.ActivePages
            .Select(p => new ExportPage(SessionPaths.Source(store.Root, session, p), p.Rotation, p.Filter, session.OptionsUsed.Color, p.Crop, session.OptionsUsed.Dpi))
            .ToArray();
        var progress = new Progress<int>(percent => _ = hub.Clients.All.ExportProgress(sessionId, percent));
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
                string token = tokens.Issue(sessionId, Path.Combine(dir, $"{Guid.NewGuid():N}.pdf"));
                tokens.TryPeekPath(token, out string path);
                await using (FileStream output = File.Create(path))
                {
                    await writer.WritePdfAsync(pages, pdf, output, progress, cancellationToken).ConfigureAwait(false);
                }

                result = new ExportResult(ExportMode.Download, pages.Length, null, $"/export/{sessionId}/{token}.pdf");
                recorded = new SessionEvent(SessionEventKind.Downloaded, time.GetUtcNow());
            }
            else
            {
                string saved = await documents.SaveAsync(
                    (stream, ct) => writer.WritePdfAsync(pages, pdf, stream, progress, ct),
                    session.CreatedAt,
                    cancellationToken).ConfigureAwait(false);
                result = new ExportResult(ExportMode.Save, pages.Length, saved, null);
                recorded = new SessionEvent(SessionEventKind.Exported, time.GetUtcNow(), saved);
                logger.LogInformation("Session {Session} saved to {Path} ({Pages} pages)", sessionId, saved, pages.Length);
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
