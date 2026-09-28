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

/// <param name="State">Readiness.</param>
/// <param name="DeviceName">Human-readable device name when one is selected.</param>
/// <param name="Message">Diagnostic text for <see cref="ScannerState.Error"/> or <see cref="ScannerState.NoDevice"/>.</param>
public sealed record ScannerStatus(ScannerState State, string? DeviceName, string? Message);
