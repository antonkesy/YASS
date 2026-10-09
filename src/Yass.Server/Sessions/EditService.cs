using Yass.Core.Domain;
using Yass.Server.Hub;

namespace Yass.Server.Sessions;

/// <summary>Applies edits to a stored session, persists, and pushes <c>PageUpdated</c> / <c>SessionChanged</c>.</summary>
public sealed class EditService(SessionStore store, SessionMapper mapper, IPageSink sink, IClientNotifier hub)
{
    /// <exception cref="InvalidOperationException">The session is not editable in its current state.</exception>
    /// <exception cref="ArgumentException">An edit is invalid.</exception>
    public async Task<Session> ApplyAsync(string sessionId, IReadOnlyList<PageEdit> edits)
    {
        Session? before = null;
        Session after = store.Update(sessionId, current =>
        {
            if (current.State is not (SessionState.Review or SessionState.Done))
            {
                throw new InvalidOperationException($"Session is {current.State}; edits are only possible in Review.");
            }

            before = current;
            return PageEdits.Apply(current, edits);
        });

        bool reordered = !before!.Pages.Select(p => p.SourceFile).SequenceEqual(after.Pages.Select(p => p.SourceFile));
        if (reordered)
        {
            await hub.All.SessionChanged(mapper.ToDto(after)).ConfigureAwait(false);
        }

        var previous = before.Pages.ToDictionary(p => p.SourceFile, StringComparer.Ordinal);
        foreach (Page page in after.Pages)
        {
            Page old = previous[page.SourceFile];
            if (old == page && !reordered)
            {
                continue;
            }

            if (old.Hash != page.Hash)
            {
                sink.Enqueue(after, page);
            }

            await hub.All.PageUpdated(after.Id, mapper.ToDto(after, page)).ConfigureAwait(false);
        }

        return after;
    }
}
