namespace Yass.Core.Export;

/// <summary>
/// What a PDF carries about itself besides its pages. Every field is optional; a writer
/// leaves out what is null. <see cref="Title"/> is the document's name, the rest is whoever
/// is scanning says it is.
/// </summary>
public sealed record DocumentMetadata(string? Title = null, string? Author = null, string? Subject = null, string? Keywords = null)
{
    public static DocumentMetadata None { get; } = new();
}
