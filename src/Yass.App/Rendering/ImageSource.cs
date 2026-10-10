using Yass.App.Sessions;
using Yass.Core.Domain;

namespace Yass.App.Rendering;

/// <summary>One derived image, pinned by hash. The same hash always means the same pixels.</summary>
public sealed record ImageRequest(Session Session, Page Page, string Hash, ImageKind Kind)
{
    /// <summary>The hash the page has now, as opposed to one an older edit left behind.</summary>
    public bool IsCurrent => Hash == Page.Hash;
}

/// <summary>What <c>/img/{sessionId}/{pageIndex}/{kind}?h=HASH</c> points to, whichever host serves it.</summary>
public sealed class ImageSource(SessionStore store, RenderCache cache)
{
    /// <summary>
    /// Validates the address as a URL spells it, <paramref name="kind"/> being <c>thumb</c>,
    /// <c>preview</c> or <c>frame</c>. An empty <paramref name="hash"/> means the page's current one.
    /// </summary>
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

    /// <summary>Validates the address. An empty <paramref name="hash"/> means the page's current one.</summary>
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

    /// <summary>
    /// The WebP file for <paramref name="request"/>, rendered if it is current and not yet cached.
    /// An older hash is served only while it is still cached: superseded edits are never re-rendered.
    /// </summary>
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
