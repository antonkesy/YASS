using Yass.App.Sessions;
using Yass.Core.Domain;

namespace Yass.App.Rendering;

/// <summary>The same hash always means the same pixels.</summary>
public sealed record ImageRequest(Session Session, Page Page, string Hash, ImageKind Kind)
{
    public bool IsCurrent => Hash == Page.Hash;
}

public sealed class ImageSource(SessionStore store, RenderCache cache)
{
    /// <summary>An empty <paramref name="hash"/> means the page's current one.</summary>
    public bool TryResolve(string sessionId, int pageIndex, string kind, string? hash, out ImageRequest request)
    {
        request = null!;
        ImageKind? parsed = kind switch
        {
            "thumb" => ImageKind.Thumb,
            "preview" => ImageKind.Preview,
            "frame" => ImageKind.Frame,
            _ => null,
        };
        return parsed is { } k && TryResolve(sessionId, pageIndex, hash, k, out request);
    }

    /// <summary>An empty <paramref name="hash"/> means the page's current one.</summary>
    public bool TryResolve(string sessionId, int pageIndex, string? hash, ImageKind kind, out ImageRequest request)
    {
        request = null!;
        if (!SessionPaths.IsValidId(sessionId) || !store.TryGet(sessionId, out Session session) || pageIndex < 0 || pageIndex >= session.Pages.Count)
        {
            return false;
        }

        Page page = session.Pages[pageIndex];
        string wanted = string.IsNullOrEmpty(hash) ? page.Hash : hash;
        if (!wanted.All(char.IsAsciiHexDigitLower))
        {
            return false;
        }

        request = new ImageRequest(session, page, wanted, kind);
        return true;
    }

    // An older hash is served only while still cached: superseded edits are never re-rendered.
    public async Task<string?> GetFileAsync(ImageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IsCurrent)
        {
            return await cache.GetOrRenderAsync(request.Session, request.Page, request.Kind, cancellationToken).ConfigureAwait(false);
        }

        string path = cache.PathFor(request.Session.Id, request.Hash, request.Kind);
        return File.Exists(path) ? path : null;
    }
}
