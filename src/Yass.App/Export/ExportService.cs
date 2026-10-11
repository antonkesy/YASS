using Yass.App.Api;
using Yass.App.Config;
using Yass.App.Rendering;
using Yass.App.Sessions;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.App.Export;

public sealed class ExportService(
    SessionStore store,
    SessionMapper mapper,
    ExportDocumentWriter writer,
    ImageRenderer renderer,
    ConfigStore config,
    TimeProvider time,
    IClientNotifier hub,
    ILogger<ExportService> logger)
{
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

        try
        {
            ExportPlan plan = ExportPlan.Create(store.Root, session, request);
            // The writer decodes one page at a time; that counts against max_concurrent_renders (SPEC §9).
            using IDisposable slot = await renderer.ReserveAsync(cancellationToken).ConfigureAwait(false);
            string dir = request.ToServer ? output.Directory : SessionPaths.Export(store.Root, sessionId);
            var produced = new List<ExportedDocumentDto>();
            for (int d = 0; d < plan.Documents.Length; d++)
            {
                // Synchronous reporting preserves ordering between documents.
                int index = d;
                var progress = new InlineProgress(percent =>
                    _ = hub.All.ExportProgress(sessionId, Math.Clamp((int)((index + percent / 100.0) / plan.Documents.Length * 100), 0, 100)));
                produced.Add(await writer.WriteAsync(sessionId, plan.Documents[d], request.Format,
                    output.Pdf, dir, request.ToServer, progress, cancellationToken).ConfigureAwait(false));
            }

            var result = new ExportResult(plan.PageCount, [.. produced]);
            var recorded = request.ToServer
                ? new SessionEvent(SessionEventKind.Exported, time.GetUtcNow(), produced[0].SavedTo)
                : new SessionEvent(SessionEventKind.Downloaded, time.GetUtcNow());
            logger.LogInformation(
                "Session {Session} exported as {Documents} document(s), {Pages} page(s) total",
                sessionId,
                produced.Count,
                plan.PageCount);

            await hub.All.ExportProgress(sessionId, 100).ConfigureAwait(false);
            Session done = store.Update(sessionId, s => (s with { State = SessionState.Done }).With(recorded));
            await hub.All.SessionChanged(mapper.ToDto(done)).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            // On cancel too, or the session would stay Exporting until a restart.
            bool cancelled = ex is OperationCanceledException;
            if (!cancelled)
            {
                logger.LogError(ex, "Export of {Session} failed", sessionId);
            }

            Session back = store.Update(sessionId, s => s with { State = SessionState.Review, Error = cancelled ? null : ex.Message });
            await hub.All.SessionChanged(mapper.ToDto(back)).ConfigureAwait(false);
            throw;
        }
    }

    private sealed class InlineProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
