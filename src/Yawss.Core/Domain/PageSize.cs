using Tapper;

namespace Yawss.Core.Domain;

/// <summary>Physical page size requested from the scanner.</summary>
[TranspilationSource]
public enum PageSize
{
    A4 = 0,
    A5,
    Letter,
    Legal,
}
