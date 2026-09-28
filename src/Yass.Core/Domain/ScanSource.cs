using Tapper;

namespace Yass.Core.Domain;

/// <summary>Paper source used for a scan.</summary>
[TranspilationSource]
public enum ScanSource
{
    Flatbed = 0,
    Feeder,
    Duplex,
}
