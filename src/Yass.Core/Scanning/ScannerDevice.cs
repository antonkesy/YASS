namespace Yass.Core.Scanning;

/// <summary>
/// One scanner the backend can see. <paramref name="Id"/> is what a scan asks for and is the
/// driver's own identifier; <paramref name="Name"/> is what a person reads.
/// </summary>
/// <param name="Id">The driver's identifier, what a scan asks for.</param>
/// <param name="Name">What the driver calls it.</param>
/// <param name="Driver">The driver that found it — sane, escl, … — when the backend says.</param>
/// <param name="Connection">Where it is reached, e.g. an eSCL address, when the driver reports one.</param>
public sealed record ScannerDevice(string Id, string Name, string? Driver = null, string? Connection = null);
