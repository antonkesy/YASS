namespace Yass.Server.Hosting;

public static class StaticFrontend
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        string root = app.Environment.WebRootPath;
        if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "index.html")))
        {
            app.Logger.LogWarning("No frontend build found in {Root}; only the API is served", root ?? "wwwroot");
            app.MapGet("/", () => Results.Text("YASS API is running; frontend build not deployed.", "text/plain"));
            return;
        }

        StaticFileOptions options = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                bool immutable = ctx.Context.Request.Path.StartsWithSegments("/_app/immutable");
                ctx.Context.Response.Headers.CacheControl = immutable ? "public, max-age=31536000, immutable" : "no-cache";
            },
        };

        app.UseDefaultFiles();
        app.UseStaticFiles(options);

        // Same options: every deep link is served by the fallback, not the static middleware.
        app.MapFallbackToFile("index.html", options);
    }
}
