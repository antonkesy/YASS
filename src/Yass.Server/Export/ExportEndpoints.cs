using Microsoft.AspNetCore.Http.HttpResults;
using Yass.App.Export;

namespace Yass.Server.Export;

/// <summary>Serves a produced document once, then deletes it.</summary>
public static class ExportEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/export/{sessionId}/{file}", Results<PhysicalFileHttpResult, NotFound> (string sessionId, string file, HttpContext ctx, ExportDownloads downloads) =>
        {
            if (!downloads.TryRedeem(sessionId, file, out ExportDownload download))
            {
                return TypedResults.NotFound();
            }

            ctx.Response.OnCompleted(() =>
            {
                File.Delete(download.Path);
                return Task.CompletedTask;
            });
            ctx.Response.Headers.CacheControl = "no-store";
            return TypedResults.PhysicalFile(download.Path, download.ContentType, fileDownloadName: download.DownloadName);
        });
    }
}
