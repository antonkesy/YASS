using Yass.App.Rendering;
using Yass.Core.Domain;

namespace Yass.App.Sessions;

/// <summary>
/// Crops away the frame a scanner leaves around a page. Detection needs the pixels, which the
/// edit model never sees, so it happens here first; what it finds is applied as ordinary crop
/// edits, with everything <see cref="EditService"/> does for any other edit.
/// </summary>
public sealed class AutoCropService(SessionStore store, ImageRenderer renderer, EditService editor)
{
    /// <summary>Crops the frame off each target page that has one. Returns how many did.</summary>
    public async Task<int> CropAsync(string sessionId, IReadOnlyList<int>? targets, CancellationToken cancellationToken)
    {
        Session session = store.Get(sessionId);
        if (session.State is not (SessionState.Review or SessionState.Done))
        {
            // Checked up front as well as in the edit: detection reads every target page, which is
            // not worth doing for an edit that will be refused.
            throw new InvalidOperationException($"Session is {session.State}; edits are only possible in Review.");
        }

        IReadOnlyList<int> indices = targets is { Count: > 0 } ? targets : Enumerable.Range(0, session.Pages.Count).ToArray();
        // Detection takes a while and pages can be reordered meanwhile, so what it finds is kept
        // by page file and only turned into positions when the crops are applied.
        var found = new Dictionary<string, CropBox>(StringComparer.Ordinal);
        foreach (int index in indices)
        {
            if (index < 0 || index >= session.Pages.Count)
            {
                throw new ArgumentException($"Page index {index} is out of range (0..{session.Pages.Count - 1}).", nameof(targets));
            }

            Page page = session.Pages[index];
            CropBox? box = await renderer.DetectBordersAsync(SessionPaths.Source(store.Root, session, page), cancellationToken).ConfigureAwait(false);
            if (box is not null)
            {
                found[page.SourceFile] = box;
            }
        }

        int cropped = 0;
        if (found.Count > 0)
        {
            await editor.ApplyAsync(sessionId, current =>
            {
                PageEdit[] edits =
                [
                    .. current.Pages
                        .Select((page, index) => (page, index, box: found.GetValueOrDefault(page.SourceFile)))
                        .Where(x => x.box is not null && x.box != x.page.Crop)
                        .Select(x => PageEdit.SetCrop(x.box, x.index)),
                ];
                cropped = edits.Length;
                return edits;
            }).ConfigureAwait(false);
        }

        return cropped;
    }
}
