namespace Yawss.Core.Scanning;

/// <summary>One page produced by <see cref="IScannerBackend.ScanAsync"/>.</summary>
/// <param name="Number">1-based page number within this scan run.</param>
/// <param name="Png">Encoded PNG bytes. The caller writes them to disk and drops the reference.</param>
/// <param name="IsBlank">Set by the backend's blank-page detection when enabled in the options.</param>
public sealed record ScannedPage(int Number, byte[] Png, bool IsBlank);
