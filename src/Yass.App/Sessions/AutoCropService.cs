using Yass.App.Rendering;
using Yass.Core.Domain;

namespace Yass.App.Sessions;

public sealed class AutoCropService(SessionStore store, ImageRenderer renderer, EditService editor)
{
    public async Task<int> CropAsync(string sessionId, IReadOnlyList<int>? targets, CancellationToken cancellationToken)
    {
        Session session = store.Get(sessionId);
        if (session.State is not (SessionState.Review or SessionState.Done))
        {
            // Checked up front too: detection reads every target page, not worth it for a refused edit.
            throw new InvalidOperationException($"Session is {session.State}; edits are only possible in Review.");
        }

        IReadOnlyList<int> indices = targets is { Count: > 0 } ? targets : Enumerable.Range(0, session.Pages.Count).ToArray();
        // Pages can be reordered during detection, so results are keyed by page file until applied.
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
