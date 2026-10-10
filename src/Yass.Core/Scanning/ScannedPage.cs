namespace Yass.Core.Scanning;

public sealed record ScannedPage(int Number, byte[] Png, bool IsBlank);
