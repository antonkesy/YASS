using Tapper;

namespace Yass.Core.Domain;

/// <summary>
/// A non-destructive edit applied to one, several, or all pages of a session.
/// Modelled as a flat record (op + optional payload) rather than a class hierarchy so it
/// serialises unchanged over MessagePack and generates a plain TypeScript type.
/// </summary>
[TranspilationSource]
public sealed record PageEdit
{
    public required PageEditOp Op { get; init; }

    /// <summary>Page indices the edit applies to; <c>null</c> means all pages.</summary>
    public IReadOnlyList<int>? Targets { get; init; }

    /// <summary>Payload for <see cref="PageEditOp.Rotate"/>: a multiple of 90, positive or negative.</summary>
    public int Degrees { get; init; }

    /// <summary>Payload for <see cref="PageEditOp.SetFilter"/>.</summary>
    public PageFilter Filter { get; init; }

    /// <summary>Payload for <see cref="PageEditOp.SetCrop"/>; <c>null</c> clears the crop.</summary>
    public CropBox? Crop { get; init; }

    /// <summary>Payload for <see cref="PageEditOp.SetSplit"/>: whether the page begins a document.</summary>
    public bool Split { get; init; }

    /// <summary>Payload for <see cref="PageEditOp.Reorder"/>: a permutation of every page index.</summary>
    public IReadOnlyList<int>? Order { get; init; }

    public static PageEdit Rotate(int degrees, params int[] targets) =>
        new() { Op = PageEditOp.Rotate, Degrees = degrees, Targets = ToTargets(targets) };

    public static PageEdit Delete(params int[] targets) =>
        new() { Op = PageEditOp.Delete, Targets = ToTargets(targets) };

    public static PageEdit Restore(params int[] targets) =>
        new() { Op = PageEditOp.Restore, Targets = ToTargets(targets) };

    public static PageEdit SetFilter(PageFilter filter, params int[] targets) =>
        new() { Op = PageEditOp.SetFilter, Filter = filter, Targets = ToTargets(targets) };

    public static PageEdit SetCrop(CropBox? crop, params int[] targets) =>
        new() { Op = PageEditOp.SetCrop, Crop = crop, Targets = ToTargets(targets) };

    public static PageEdit SetSplit(bool split, params int[] targets) =>
        new() { Op = PageEditOp.SetSplit, Split = split, Targets = ToTargets(targets) };

    public static PageEdit Reorder(params int[] order) =>
        new() { Op = PageEditOp.Reorder, Order = order };

    /// <summary>An empty target list means "all pages".</summary>
    private static int[]? ToTargets(int[] targets) => targets.Length == 0 ? null : targets;
}
