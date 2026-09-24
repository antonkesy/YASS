using Tapper;

namespace Yawss.Core.Domain;

/// <summary>Lifecycle of a scan session.</summary>
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
