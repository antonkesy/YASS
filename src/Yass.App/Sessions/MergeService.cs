using Yass.Core.Domain;

namespace Yass.App.Sessions;

/// <summary>Adds the active pages of one session to the end of another, as a document of their own.</summary>
public sealed class MergeService(SessionStore store, IPageSink sink, ILogger<MergeService> logger)
{
    /// <exception cref="InvalidOperationException">The target is busy, is the source, or the source has no pages left.</exception>
    public Session Merge(string targetId, string sourceId)
    {
        EnsureEditable(store.Get(targetId));
        if (string.Equals(targetId, sourceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A scan cannot be merged into itself.");
        }

        Session source = store.Get(sourceId);
        Page[] incoming = [.. source.ActivePages];
        if (incoming.Length == 0)
        {
            throw new InvalidOperationException("That scan has no pages left to add.");
        }

        // Page files are copied, not moved: the source scan stays intact and usable.
        string targetDir = SessionPaths.Directory(store.Root, targetId);
        var copies = new List<(Page Page, string File)>();
        Session merged;
        try
        {
            foreach (Page page in incoming)
            {
                string fileName = $"merged-{Guid.NewGuid():N}{Path.GetExtension(page.SourceFile)}";
                File.Copy(SessionPaths.Source(store.Root, source, page), Path.Combine(targetDir, fileName), overwrite: false);
                copies.Add((page, fileName));
            }

            // Indexed against the session as it is now, not as it was before the copy: a scan
            // could have added pages meanwhile.
            merged = store.Update(targetId, s =>
            {
                EnsureEditable(s);
                return s with
                {
                    Pages =
                    [
                        .. s.Pages,
                        .. copies.Select((c, i) => PageHash.Rehash(
                            c.Page with
                            {
                                Index = s.Pages.Count + i,
                                SourceFile = c.File,
                                // The merged run becomes its own document, which is the point of
                                // merging rather than rescanning into this session.
                                StartsDocument = i == 0,
                                Deleted = false,
                            },
                            s.OptionsUsed.Color)),
                    ],
                };
            });
        }
        catch
        {
            foreach ((_, string file) in copies)
            {
                File.Delete(Path.Combine(targetDir, file));
            }

            throw;
        }

        var added = copies.Select(c => c.File).ToHashSet(StringComparer.Ordinal);
        foreach (Page page in merged.Pages.Where(p => added.Contains(p.SourceFile)))
        {
            sink.Enqueue(merged, page);
        }

        logger.LogInformation("Merged {Count} page(s) from {Source} into {Target}", copies.Count, sourceId, targetId);
        return merged;
    }

    private static void EnsureEditable(Session target)
    {
        if (target.State is not (SessionState.Review or SessionState.Done))
        {
            throw new InvalidOperationException($"Session is {target.State}; pages can only be added in Review.");
        }
    }
}
