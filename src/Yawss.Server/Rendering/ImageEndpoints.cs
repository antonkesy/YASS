using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using Yawss.Core.Domain;
using Yawss.Server.Sessions;

namespace Yawss.Server.Rendering;

/// <summary>
/// <c>GET /img/{sessionId}/{pageIndex}/thumb|preview?h=HASH</c>. The hash pins the exact derived image, so the
/// response is immutable and the browser never revalidates; a new hash is a new URL.
/// </summary>
public static class ImageEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/img/{sessionId}/{pageIndex:int}/thumb", (string sessionId, int pageIndex, string? h, HttpContext ctx, SessionStore store, RenderCache cache, CancellationToken ct) =>
            ServeAsync(sessionId, pageIndex, h, ImageKind.Thumb, ctx, store, cache, ct));
        app.MapGet("/img/{sessionId}/{pageIndex:int}/preview", (string sessionId, int pageIndex, string? h, HttpContext ctx, SessionStore store, RenderCache cache, CancellationToken ct) =>
            ServeAsync(sessionId, pageIndex, h, ImageKind.Preview, ctx, store, cache, ct));
    }

    private static async Task<Results<PhysicalFileHttpResult, NotFound, StatusCodeHttpResult>> ServeAsync(
        string sessionId, int pageIndex, string? hash, ImageKind kind, HttpContext ctx, SessionStore store, RenderCache cache, CancellationToken ct)
    {
        if (!SessionPaths.IsValidId(sessionId) || !store.TryGet(sessionId, out Session session) || pageIndex < 0 || pageIndex >= session.Pages.Count)
        {
            return TypedResults.NotFound();
        }

        Page page = session.Pages[pageIndex];
        string wanted = string.IsNullOrEmpty(hash) ? page.Hash : hash;
        if (!wanted.All(char.IsAsciiHexDigitLower))
        {
            return TypedResults.NotFound();
        }

        string etag = $"\"{wanted}\"";
        if (ctx.Request.Headers.IfNoneMatch.Any(v => v == etag))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        string path;
        if (wanted == page.Hash)
        {
            path = await cache.GetOrRenderAsync(session, page, kind, ct).ConfigureAwait(false);
        }
        else
        {
            // An older hash: serve it only if it is still cached; never re-render superseded edits.
            path = cache.PathFor(sessionId, wanted, kind);
            if (!File.Exists(path))
            {
                return TypedResults.NotFound();
            }
        }

        ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return TypedResults.PhysicalFile(path, "image/webp", entityTag: new EntityTagHeaderValue(etag));
    }
}
