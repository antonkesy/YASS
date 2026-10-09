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
        var edits = new List<PageEdit>();
        foreach (int index in indices)
        {
            if (index < 0 || index >= session.Pages.Count)
            {
                throw new ArgumentException($"Page index {index} is out of range (0..{session.Pages.Count - 1}).", nameof(targets));
            }

            Page page = session.Pages[index];
            CropBox? box = await renderer.DetectBordersAsync(SessionPaths.Source(store.Root, session, page), cancellationToken).ConfigureAwait(false);
            if (box is not null && box != page.Crop)
            {
                edits.Add(PageEdit.SetCrop(box, index));
            }
        }

        if (edits.Count > 0)
        {
            await editor.ApplyAsync(sessionId, edits).ConfigureAwait(false);
        }

        return edits.Count;
    }
}
