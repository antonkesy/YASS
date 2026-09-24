namespace Yawss.Server.Hosting;

/// <summary>Serves the SvelteKit build from <c>wwwroot</c> with SPA fallback. Harmless when the folder is absent.</summary>
public static class StaticFrontend
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        string root = app.Environment.WebRootPath;
        if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "index.html")))
        {
            app.Logger.LogWarning("No frontend build found in {Root}; only the API is served", root ?? "wwwroot");
            app.MapGet("/", () => Results.Text("YAWSS API is running; frontend build not deployed.", "text/plain"));
            return;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // Hashed immutable assets vs. everything else (index.html, manifest, service worker).
                bool immutable = ctx.Context.Request.Path.StartsWithSegments("/_app/immutable");
                ctx.Context.Response.Headers.CacheControl = immutable ? "public, max-age=31536000, immutable" : "no-cache";
            },
        });
        app.MapFallbackToFile("index.html");
    }
}
