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

/// <summary>Suggests file names for a scanned session.</summary>
public interface INameSuggester
{
    /// <summary>Whether anything beyond the template is available; the UI hides Find name when not.</summary>
    bool CanRead { get; }

    /// <param name="pageImagePaths">Source images to read, in page order.</param>
    /// <param name="fallback">Name to offer when nothing better is found, e.g. the expanded template.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    Task<IReadOnlyList<NameSuggestion>> SuggestAsync(
        IReadOnlyList<string> pageImagePaths,
        string fallback,
        CancellationToken cancellationToken);
}
