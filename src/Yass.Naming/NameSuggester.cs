using System.Globalization;
using Microsoft.Extensions.Logging;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Naming;

namespace Yass.Naming;

/// <summary>
/// Puts together what the three sources can offer: the template, the biggest line of text
/// on the page, and a model. Every stage is optional and failing — no tesseract, no model,
/// an unreadable page — degrades to fewer suggestions rather than an error.
/// </summary>
public sealed class NameSuggester(
    TesseractTextReader reader,
    AiNamer ai,
    Func<NamingConfig> config,
    ILogger<NameSuggester> logger) : INameSuggester
{
    public bool CanRead => config().Ocr && reader.Available;

    public async Task<IReadOnlyList<NameSuggestion>> SuggestAsync(
        IReadOnlyList<string> pageImagePaths,
        string fallback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageImagePaths);
        NamingConfig naming = config();
        var suggestions = new List<NameSuggestion>();

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            suggestions.Add(new NameSuggestion(
                fallback,
                NameSource.Template,
                "From your file name template",
                0.5));
        }

        if (!CanRead)
        {
            return suggestions;
        }

        var lines = new List<PageLine>();
        foreach (string path in pageImagePaths.Take(naming.OcrPages))
        {
            lines.AddRange(await reader.ReadLinesAsync(path, naming, cancellationToken).ConfigureAwait(false));
        }

        if (lines.Count == 0)
        {
            logger.LogInformation("No text found on the first {Pages} page(s)", naming.OcrPages);
            return suggestions;
        }

        if (Heading(lines) is { } heading)
        {
            suggestions.Insert(0, heading);
        }

        string? written = await ai.SuggestAsync(
            string.Join('\n', lines.Select(l => l.Text)),
            naming,
            cancellationToken).ConfigureAwait(false);

        if (written is not null)
        {
            suggestions.Insert(0, new NameSuggestion(
                written,
                NameSource.Ai,
                $"Summarised from the page text by {naming.Model}",
                0.9));
        }

        return suggestions;
    }

    /// <summary>
    /// The largest line in the top third of the first page, which is where a document puts
    /// its title. Confidence rises with how far it stands out from the body text.
    /// </summary>
    internal static NameSuggestion? Heading(IReadOnlyList<PageLine> lines)
    {
        int cutoff = lines.Max(l => l.Top) / 3;
        List<PageLine> candidates = [.. lines.Where(l => l.Top <= cutoff && Plausible(l.Text))];
        if (candidates.Count == 0)
        {
            return null;
        }

        PageLine best = candidates.MaxBy(l => l.FontSize)!;
        double median = Median([.. lines.Select(l => (double)l.FontSize)]);
        double ratio = median <= 0 ? 1 : best.FontSize / median;

        return new NameSuggestion(
            Tidy(best.Text),
            NameSource.Ocr,
            ratio >= 1.4
                ? "The largest heading on the first page"
                : "The first prominent line of text",
            Math.Clamp(0.45 + (ratio - 1) * 0.5, 0.3, 0.88));
    }

    /// <summary>Rejects page furniture: numbers, dates alone, and lines too short or too long to be a title.</summary>
    private static bool Plausible(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length is >= 4 and <= 90
            && trimmed.Any(char.IsLetter)
            && trimmed.Count(char.IsLetter) >= trimmed.Length / 3;
    }

    private static string Tidy(string text)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(FileNames.Strip(text).ToLowerInvariant());
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
    }
}
