using Tapper;

namespace Yass.Core.Domain;

/// <summary>Flat record rather than a class hierarchy so it serialises over MessagePack and generates a plain TypeScript type.</summary>
[TranspilationSource]
public sealed record PageEdit
{
    public required PageEditOp Op { get; init; }

    /// <summary><c>null</c> means all pages.</summary>
    public IReadOnlyList<int>? Targets { get; init; }

    public int Degrees { get; init; }

    public PageFilter Filter { get; init; }

    public CropBox? Crop { get; init; }

    public bool Split { get; init; }

    public IReadOnlyList<int>? Order { get; init; }

    public PageAdjust? Adjust { get; init; }

    public PageCleanup Cleanup { get; init; }

    public double Skew { get; init; }

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

    public static PageEdit SetAdjust(PageAdjust? adjust, params int[] targets) =>
        new() { Op = PageEditOp.SetAdjust, Adjust = adjust, Targets = ToTargets(targets) };

    public static PageEdit AddCleanup(PageCleanup cleanup, params int[] targets) =>
        new() { Op = PageEditOp.AddCleanup, Cleanup = cleanup, Targets = ToTargets(targets) };

    public static PageEdit RemoveCleanup(PageCleanup cleanup, params int[] targets) =>
        new() { Op = PageEditOp.RemoveCleanup, Cleanup = cleanup, Targets = ToTargets(targets) };

    public static PageEdit SetSkew(double degrees, params int[] targets) =>
        new() { Op = PageEditOp.SetSkew, Skew = degrees, Targets = ToTargets(targets) };

    public static PageEdit Reorder(params int[] order) =>
        new() { Op = PageEditOp.Reorder, Order = order };

    private static int[]? ToTargets(int[] targets) => targets.Length == 0 ? null : targets;
}
