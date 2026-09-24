namespace Yawss.Server.Hosting;

/// <summary>Serves the SvelteKit build from <c>wwwroot</c> with SPA fallback. Harmless when the folder is absent.</summary>
public static class StaticFrontend
{
    public const string WebRootEnv = "YAWSS_WEBROOT";

    /// <summary>
    /// <c>YAWSS_WEBROOT</c> if set, else <c>wwwroot</c> next to the binary (publish/Docker layout), else
    /// <c>wwwroot</c> in the working directory (<c>dotnet run</c>).
    /// </summary>
    public static string ResolveWebRoot()
    {
        string? fromEnv = Environment.GetEnvironmentVariable(WebRootEnv);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        string beside = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        return Directory.Exists(beside) ? beside : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

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
