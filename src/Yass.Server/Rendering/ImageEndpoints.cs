using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using Yass.App.Rendering;

namespace Yass.Server.Rendering;

/// <summary>The hash pins the exact derived image, so responses are immutable; a new hash is a new URL.</summary>
public static class ImageEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/img/{sessionId}/{pageIndex:int}/{kind}", ServeAsync);
    }

    private static async Task<Results<PhysicalFileHttpResult, NotFound, StatusCodeHttpResult>> ServeAsync(
        string sessionId, int pageIndex, string kind, string? h, HttpContext ctx, ImageSource images, CancellationToken ct)
    {
        if (!images.TryResolve(sessionId, pageIndex, kind, h, out ImageRequest request))
        {
            return TypedResults.NotFound();
        }

        string etag = $"\"{request.Hash}\"";
        if (ctx.Request.Headers.IfNoneMatch.Any(v => v == etag))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        string? path = await images.GetFileAsync(request, ct).ConfigureAwait(false);
        if (path is null)
        {
            return TypedResults.NotFound();
        }

        ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return TypedResults.PhysicalFile(path, "image/webp", entityTag: new EntityTagHeaderValue(etag));
    }
}
