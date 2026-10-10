namespace Yass.Core.Scanning;

public sealed record ScannerDevice(string Id, string Name, string? Driver = null, string? Connection = null);
