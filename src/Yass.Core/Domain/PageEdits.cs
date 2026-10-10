namespace Yass.Core.Domain;

public static class PageEdits
{
    public static Session Apply(Session session, IReadOnlyList<PageEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(edits);

        var pages = session.Pages.ToList();
        foreach (PageEdit edit in edits)
        {
            ArgumentNullException.ThrowIfNull(edit);
            pages = edit.Op == PageEditOp.Reorder ? Reorder(pages, edit) : ApplyToTargets(pages, edit, session.OptionsUsed.Color);
        }

        return session with { Pages = pages };
    }

    private static List<Page> ApplyToTargets(List<Page> pages, PageEdit edit, ColorMode sessionColor)
    {
        IEnumerable<int> targets = edit.Targets ?? Enumerable.Range(0, pages.Count);
        foreach (int index in targets)
        {
            if (index < 0 || index >= pages.Count)
            {
                throw new ArgumentException($"Page index {index} is out of range (0..{pages.Count - 1}).", nameof(edit));
            }

            Page page = pages[index];
            Page updated = edit.Op switch
            {
                PageEditOp.Rotate => page with { Rotation = page.Rotation.Rotate(edit.Degrees) },
                PageEditOp.Delete => page with { Deleted = true },
                PageEditOp.Restore => page with { Deleted = false },
                PageEditOp.SetFilter => page with { Filter = edit.Filter },
                PageEditOp.SetCrop => page with { Crop = Validated(edit.Crop) },
                PageEditOp.SetSplit => page with { StartsDocument = edit.Split },
                PageEditOp.SetAdjust => page with { Adjust = Validated(edit.Adjust) },
                PageEditOp.AddCleanup => page with { Cleanup = page.Cleanup | edit.Cleanup },
                PageEditOp.RemoveCleanup => page with { Cleanup = page.Cleanup & ~edit.Cleanup },
                PageEditOp.SetSkew => page with { Skew = Straighten.Normalise(edit.Skew) },
                _ => throw new ArgumentException($"Unsupported edit op {edit.Op}.", nameof(edit)),
            };
            pages[index] = PageHash.Rehash(updated, sessionColor);
        }

        return pages;
    }

    private static CropBox? Validated(CropBox? crop)
    {
        if (crop is null || crop.IsEmpty())
        {
            return null;
        }

        crop.Validate();
        return crop;
    }

    private static PageAdjust? Validated(PageAdjust? adjust)
    {
        if (adjust is null || adjust.IsNeutral())
        {
            return null;
        }

        adjust.Validate();
        return adjust;
    }

    private static List<Page> Reorder(List<Page> pages, PageEdit edit)
    {
        IReadOnlyList<int> order = edit.Order ?? throw new ArgumentException("Reorder needs an order.", nameof(edit));
        if (order.Count != pages.Count || order.Distinct().Count() != pages.Count || order.Any(i => i < 0 || i >= pages.Count))
        {
            throw new ArgumentException($"Reorder must be a permutation of 0..{pages.Count - 1}.", nameof(edit));
        }

        return order.Select((oldIndex, newIndex) => pages[oldIndex] with { Index = newIndex }).ToList();
    }
}
