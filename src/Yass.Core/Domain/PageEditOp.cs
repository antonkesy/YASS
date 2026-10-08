using Tapper;

namespace Yass.Core.Domain;

/// <summary>Kind of a <see cref="PageEdit"/>.</summary>
[TranspilationSource]
public enum PageEditOp
{
    /// <summary>Rotate the target pages by <see cref="PageEdit.Degrees"/>.</summary>
    Rotate = 0,

    /// <summary>Mark the target pages as deleted.</summary>
    Delete,

    /// <summary>Clear the deleted flag on the target pages.</summary>
    Restore,

    /// <summary>Set <see cref="PageEdit.Filter"/> as the rendering treatment of the target pages.</summary>
    SetFilter,

    /// <summary>Reorder all pages to <see cref="PageEdit.Order"/>. Always targets all pages.</summary>
    Reorder,

    /// <summary>Set <see cref="PageEdit.Crop"/> on the target pages; <c>null</c> clears it.</summary>
    SetCrop,

    /// <summary>Start (or stop starting) a document at the target pages, per <see cref="PageEdit.Split"/>.</summary>
    SetSplit,

    /// <summary>Set <see cref="PageEdit.Adjust"/> as the tone and sharpness of the target pages; null clears it.</summary>
    SetAdjust,

    /// <summary>Turn on the clean-up passes in <see cref="PageEdit.Cleanup"/> on the target pages.</summary>
    AddCleanup,

    /// <summary>Turn off the clean-up passes in <see cref="PageEdit.Cleanup"/> on the target pages.</summary>
    RemoveCleanup,

    /// <summary>Set <see cref="PageEdit.Skew"/> as the fine rotation of the target pages.</summary>
    SetSkew,
}
