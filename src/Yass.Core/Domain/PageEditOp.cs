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
}
