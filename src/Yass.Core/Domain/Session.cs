namespace Yass.Core.Domain;

public sealed record Session(
    string Id,
    DateTimeOffset CreatedAt,
    SessionState State,
    IReadOnlyList<Page> Pages,
    ScanOptions OptionsUsed,
    string? Error = null)
{
    public string? Name { get; init; }

    /// <summary>Append-only; the last entry is the status, none means draft.</summary>
    public IReadOnlyList<SessionEvent> Events { get; init; } = [];

    public IEnumerable<Page> ActivePages => Pages.Where(p => !p.Deleted);

    /// <summary>The first active page always opens a document, so there is never a leading empty one.</summary>
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

    public Session With(SessionEvent @event) => this with { Events = [.. Events, @event] };
}
