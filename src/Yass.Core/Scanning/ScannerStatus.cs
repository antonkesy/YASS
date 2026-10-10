using Tapper;

namespace Yass.Core.Scanning;

[TranspilationSource]
public enum ScannerState
{
    Unknown = 0,
    NoDevice,
    Ready,
    Busy,
    Error,
}

public sealed record ScannerStatus(ScannerState State, string? DeviceName, string? Message);
