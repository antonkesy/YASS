using Tapper;

namespace Yass.Core.Domain;

/// <summary>What became of a session. The most recent one is the status the UI shows.</summary>
[TranspilationSource]
public enum SessionEventKind
{
    /// <summary>Written to the server's <c>output.directory</c>; the event's path is the file.</summary>
    Exported = 0,

    /// <summary>Written to a folder chosen in the UI. Legacy, as <see cref="Exported"/>.</summary>
    Saved,

    /// <summary>Handed to the browser as a download.</summary>
    Downloaded,

    /// <summary>Thrown away by the user, but kept in the list until retention removes it.</summary>
    Discarded,
}

/// <summary>One entry in a session's history.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="At">When it happened.</param>
/// <param name="Path">Where the document ended up, when the event produced a file.</param>
public sealed record SessionEvent(SessionEventKind Kind, DateTimeOffset At, string? Path = null);
