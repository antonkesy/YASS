namespace Yass.Core.Export;

public sealed record DocumentMetadata(string? Title = null, string? Author = null, string? Subject = null, string? Keywords = null)
{
    public static DocumentMetadata None { get; } = new();
}
