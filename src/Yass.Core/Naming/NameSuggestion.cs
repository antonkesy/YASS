using Tapper;

namespace Yass.Core.Naming;

/// <summary>Where a suggested name came from, which the UI badges.</summary>
[TranspilationSource]
public enum NameSource
{
    /// <summary>Built from the file-name template and the scan's own facts.</summary>
    Template = 0,

    /// <summary>Lifted from text read off the page.</summary>
    Ocr,

    /// <summary>Written by a model from the page text.</summary>
    Ai,
}

/// <param name="Name">The suggested file name, without extension.</param>
/// <param name="Source">What produced it.</param>
/// <param name="Reason">One line explaining where it came from, shown under the name.</param>
/// <param name="Confidence">0..1. Drives the meter beside the suggestion.</param>
[TranspilationSource]
public sealed record NameSuggestion(string Name, NameSource Source, string Reason, double Confidence);

/// <param name="Suggestions">Best first.</param>
/// <param name="Text">
/// What was read off the pages, for a client that wants to ask a model of its own. Null when
/// nothing was read.
/// </param>
/// <param name="Error">
/// Why the configured provider produced nothing, in words fit to show someone. Null when it
/// worked, or when there was no provider to fail.
/// </param>
public sealed record NameSuggestionSet(
    IReadOnlyList<NameSuggestion> Suggestions,
    string? Text,
    string? Error);

/// <summary>Suggests file names for a scanned session.</summary>
public interface INameSuggester
{
    /// <summary>Whether page text can be read at all, which decides what Find name can offer.</summary>
    bool CanRead { get; }

    /// <param name="pageImagePaths">Source images to read, in page order.</param>
    /// <param name="fallback">Name to offer when nothing better is found, e.g. the expanded template.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    Task<NameSuggestionSet> SuggestAsync(
        IReadOnlyList<string> pageImagePaths,
        string fallback,
        CancellationToken cancellationToken);
}
