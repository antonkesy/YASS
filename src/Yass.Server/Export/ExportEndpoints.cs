using Microsoft.AspNetCore.Http.HttpResults;
using Yass.Server.Sessions;

namespace Yass.Server.Export;

/// <summary><c>GET /export/{sessionId}/{token}.pdf</c>: serves a produced PDF once, then deletes it.</summary>
public static class ExportEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/export/{sessionId}/{token}.pdf", Results<PhysicalFileHttpResult, NotFound> (string sessionId, string token, HttpContext ctx, DownloadTokens tokens) =>
        {
            if (!SessionPaths.IsValidId(sessionId) || !tokens.TryRedeem(sessionId, token, out string path) || !File.Exists(path))
            {
                return TypedResults.NotFound();
            }

            ctx.Response.OnCompleted(() =>
            {
                File.Delete(path);
                return Task.CompletedTask;
            });
            ctx.Response.Headers.CacheControl = "no-store";
            return TypedResults.PhysicalFile(path, "application/pdf", fileDownloadName: $"yass-{sessionId}.pdf");
        });
    }
}
