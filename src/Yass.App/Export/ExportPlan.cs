using System.Globalization;
using Yass.App.Sessions;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;
using Yass.Export;

namespace Yass.App.Export;

/// <summary>A snapshot of the pages, names and metadata to write; planning performs no I/O.</summary>
internal sealed record ExportPlan(ExportDocument[] Documents)
{
    public int PageCount => Documents.Sum(document => document.Pages.Length);

    public static ExportPlan Create(string root, Session session, ExportRequest request)
    {
        string extension = request.Format switch
        {
            ExportFormat.Jpg => ".jpg",
            ExportFormat.Png => ".png",
            _ => ".pdf",
        };
        var facts = new TemplateFacts(
            Preset: string.IsNullOrWhiteSpace(session.Name) ? "Scan" : session.Name!,
            Counter: 1,
            Dpi: session.OptionsUsed.Dpi,
            Color: session.OptionsUsed.Color.ToString());
        ExportPage[][] pages = session.Documents
            .Select(doc => doc.Select(p => new ExportPage(
                SessionPaths.Source(root, session, p), RenderSpec.Of(p, session.OptionsUsed.Color), session.OptionsUsed.Dpi)).ToArray())
            .ToArray();
        return new ExportPlan([.. pages.Select((document, index) =>
        {
            string? name = NameFor(request.Name, session, facts, pages.Length, index);
            return new ExportDocument(document, MetadataFor(request, name),
                FileNames.Safe(name ?? Fallback(session, pages.Length, index)) + extension);
        })]);
    }

    private static DocumentMetadata MetadataFor(ExportRequest request, string? name) =>
        new(name, request.Metadata?.Author, request.Metadata?.Subject, request.Metadata?.Keywords);

    private static string? NameFor(string? requested, Session session, TemplateFacts facts, int documentCount, int index)
    {
        string? stem = !string.IsNullOrWhiteSpace(requested) ? requested!.Trim()
            : !string.IsNullOrWhiteSpace(session.Name) ? session.Name!.Trim()
            : null;

        if (stem is null)
        {
            return null;
        }

        return Numbered(FilenameTemplate.Expand(stem, session.CreatedAt, session.Id, facts, extension: ""), documentCount, index);
    }

    private static string Fallback(Session session, int documentCount, int index) =>
        Numbered("scan-" + session.CreatedAt.ToString("yyyy-MM-dd-HH-mm", CultureInfo.InvariantCulture), documentCount, index);

    private static string Numbered(string stem, int documentCount, int index) =>
        documentCount == 1 ? stem : $"{stem}-{(index + 1).ToString(CultureInfo.InvariantCulture)}";
}

internal sealed record ExportDocument(ExportPage[] Pages, DocumentMetadata Metadata, string FileName);
