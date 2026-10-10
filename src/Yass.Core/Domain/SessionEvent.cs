using Tapper;

namespace Yass.Core.Domain;

[TranspilationSource]
public enum SessionEventKind
{
    Exported = 0,

    Saved,

    Downloaded,

    Discarded,
}

public sealed record SessionEvent(SessionEventKind Kind, DateTimeOffset At, string? Path = null);
