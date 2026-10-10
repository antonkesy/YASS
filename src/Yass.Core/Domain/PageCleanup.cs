using Tapper;

namespace Yass.Core.Domain;

[TranspilationSource]
[Flags]
public enum PageCleanup
{
    None = 0,

    Despeckle = 1,

    AutoLevels = 2,
}
