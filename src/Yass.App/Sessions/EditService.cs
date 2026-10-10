using Yass.App.Api;
using Yass.Core.Domain;

namespace Yass.App.Sessions;

public sealed class EditService(SessionStore store, SessionMapper mapper, IPageSink sink, IClientNotifier hub)
{
    public Task<Session> ApplyAsync(string sessionId, IReadOnlyList<PageEdit> edits) => ApplyAsync(sessionId, _ => edits);

    /// <summary>Edits are worked out from the session as it is when applied, for targets known by page that may have moved.</summary>
    public async Task<Session> ApplyAsync(string sessionId, Func<Session, IReadOnlyList<PageEdit>> edits)
    {
        Session? before = null;
        Session after = store.Update(sessionId, current =>
        {
            if (current.State is not (SessionState.Review or SessionState.Done))
            {
                throw new InvalidOperationException($"Session is {current.State}; edits are only possible in Review.");
            }

            before = current;
            return PageEdits.Apply(current, edits(current));
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
