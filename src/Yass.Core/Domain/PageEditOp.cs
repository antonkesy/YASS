using Tapper;

namespace Yass.Core.Domain;

[TranspilationSource]
public enum PageEditOp
{
    Rotate = 0,

    Delete,

    Restore,

    SetFilter,

    Reorder,

    SetCrop,

    SetSplit,

    SetAdjust,

    AddCleanup,

    RemoveCleanup,

    SetSkew,
}
