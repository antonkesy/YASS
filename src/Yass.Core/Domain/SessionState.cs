using Tapper;

namespace Yass.Core.Domain;

[TranspilationSource]
public enum SessionState
{
    Idle = 0,
    Scanning,
    Review,
    Exporting,
    Done,
    Failed,
}
