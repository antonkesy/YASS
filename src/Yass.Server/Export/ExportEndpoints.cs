using Microsoft.AspNetCore.Http.HttpResults;
using Yass.Server.Sessions;

namespace Yass.Server.Export;

/// <summary>
/// <c>GET /export/{sessionId}/{file}</c>: serves a produced document once, then deletes it.
/// The extension is the format's own, not always <c>.pdf</c>: an image export is served here too.
/// </summary>
public static class ExportEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/export/{sessionId}/{file}", Results<PhysicalFileHttpResult, NotFound> (string sessionId, string file, HttpContext ctx, DownloadTokens tokens) =>
        {
            string extension = Path.GetExtension(file);
            string? contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                _ => null,
            };

            if (contentType is null
                || !SessionPaths.IsValidId(sessionId)
                || !tokens.TryRedeem(sessionId, Path.GetFileNameWithoutExtension(file), out string path)
                || !File.Exists(path))
            {
                return TypedResults.NotFound();
            }

            ctx.Response.OnCompleted(() =>
            {
                File.Delete(path);
                return Task.CompletedTask;
            });
            ctx.Response.Headers.CacheControl = "no-store";
            return TypedResults.PhysicalFile(path, contentType, fileDownloadName: $"yass-{sessionId}{extension}");
        });
    }
}
