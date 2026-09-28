namespace Yass.Core.Domain;

/// <summary>A scan session: a directory of source pages plus the edits applied to them.</summary>
/// <param name="Id">Opaque, URL-safe identifier; also the session directory name.</param>
/// <param name="CreatedAt">When the scan was started.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="Pages">Pages in display order.</param>
/// <param name="OptionsUsed">Effective options the scan was started with.</param>
/// <param name="Error">Why the session is <see cref="SessionState.Failed"/>; otherwise <c>null</c>.</param>
public sealed record Session(
    string Id,
    DateTimeOffset CreatedAt,
    SessionState State,
    IReadOnlyList<Page> Pages,
    ScanOptions OptionsUsed,
    string? Error = null)
{
    /// <summary>What the user called this scan. Null until it is renamed.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Append-only history. The last entry is the session's status; no entries means it is
    /// still a draft. Kept as events rather than a status field so "exported, then also
    /// downloaded" stays recoverable.
    /// </summary>
    public IReadOnlyList<SessionEvent> Events { get; init; } = [];

    /// <summary>Pages that will be exported, in order.</summary>
    public IEnumerable<Page> ActivePages => Pages.Where(p => !p.Deleted);

    /// <summary>
    /// Active pages grouped into documents: one group per split point, each exported as its
    /// own file. The first active page always opens a document, whatever its flag says, so a
    /// session can never produce a leading empty one.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Page>> Documents
    {
        get
        {
            var documents = new List<List<Page>>();
            foreach (Page page in ActivePages)
            {
                if (documents.Count == 0 || page.StartsDocument)
                {
                    documents.Add([]);
                }

                documents[^1].Add(page);
            }

            return documents;
        }
    }

    /// <summary>Records <paramref name="event"/> as the session's newest history entry.</summary>
    public Session With(SessionEvent @event) => this with { Events = [.. Events, @event] };
}
