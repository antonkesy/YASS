using Yass.App.Export;
using Yass.App.Rendering;

namespace Yass.Desktop;

/// <summary>A response to one <c>yass://</c> request. The stream is the webview's to close.</summary>
public sealed record SchemeResponse(Stream Body, string ContentType);

/// <summary>
/// <c>yass://app/...</c>: the same paths the server serves over HTTP, read straight off disk.
/// The static build, <c>/img/{session}/{page}/{kind}?h=</c> and <c>/export/{session}/{file}</c>;
/// anything else that is not a file is a client-side route and gets <c>index.html</c>.
/// </summary>
public sealed class AppScheme(string webRoot, ImageSource images, ExportDownloads downloads)
{
    public const string Scheme = "yass";
    public const string Origin = "yass://app";

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json",
        [".webmanifest"] = "application/manifest+json",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".webp"] = "image/webp",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8",
    };

    private readonly string _root = Path.GetFullPath(webRoot);

    /// <summary><c>null</c> when nothing is there, which the window turns into an empty 404-like answer.</summary>
    public SchemeResponse? Resolve(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Scheme)
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        return segments switch
        {
            ["img", string session, string page, string kind] => Image(session, page, kind, Query(uri, "h"), cancellationToken),
            ["export", string session, string file] => Export(session, file),
            _ => Static(segments),
        };
    }

    private SchemeResponse? Image(string sessionId, string page, string kind, string? hash, CancellationToken cancellationToken)
    {
        ImageKind? parsed = kind switch
        {
            "thumb" => ImageKind.Thumb,
            "preview" => ImageKind.Preview,
            "frame" => ImageKind.Frame,
            _ => null,
        };

        if (parsed is null
            || !int.TryParse(page, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index)
            || !images.TryResolve(sessionId, index, hash, parsed.Value, out ImageRequest request))
        {
            return null;
        }

        // The webview asks synchronously. Thumbnails are rendered ahead by the worker, so this
        // only waits on a preview or frame that nobody has looked at yet.
        string? path = images.GetFileAsync(request, cancellationToken).GetAwaiter().GetResult();
        return path is null ? null : new SchemeResponse(File.OpenRead(path), "image/webp");
    }

    private SchemeResponse? Export(string sessionId, string file) =>
        downloads.TryRedeem(sessionId, file, out ExportDownload download)
            // One-shot, as over HTTP: the file goes once the webview has read it.
            ? new SchemeResponse(new FileStream(download.Path, FileMode.Open, FileAccess.Read, FileShare.Delete, 81920, FileOptions.DeleteOnClose), download.ContentType)
            : null;

    private SchemeResponse? Static(string[] segments)
    {
        string candidate = Path.GetFullPath(Path.Combine([_root, .. segments]));
        bool inside = candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || candidate == _root;
        if (!inside)
        {
            return null;
        }

        if (!File.Exists(candidate))
        {
            // A deep link such as /session/abc, or the root itself: the SPA routes it.
            if (Path.HasExtension(candidate) && candidate != _root)
            {
                return null;
            }

            candidate = Path.Combine(_root, "index.html");
            if (!File.Exists(candidate))
            {
                return null;
            }
        }

        string type = ContentTypes.GetValueOrDefault(Path.GetExtension(candidate), "application/octet-stream");
        return new SchemeResponse(File.OpenRead(candidate), type);
    }

    private static string? Query(Uri uri, string key)
    {
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0 && pair.AsSpan(0, eq).SequenceEqual(key))
            {
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return null;
    }
}
