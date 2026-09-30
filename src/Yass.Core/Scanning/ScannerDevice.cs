namespace Yass.Core.Scanning;

/// <summary>
/// One scanner the backend can see. <paramref name="Id"/> is what a scan asks for and is the
/// driver's own identifier; <paramref name="Name"/> is what a person reads.
/// </summary>
public sealed record ScannerDevice(string Id, string Name);
