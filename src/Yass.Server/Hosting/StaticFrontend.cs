namespace Yass.Server.Hosting;

/// <summary>Serves the SvelteKit build from <c>wwwroot</c> with SPA fallback. Harmless when the folder is absent.</summary>
public static class StaticFrontend
{
    public const string WebRootEnv = "YASS_WEBROOT";

    /// <summary>
    /// <c>YASS_WEBROOT</c> if set, else <c>wwwroot</c> next to the binary (publish/Docker layout), else
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

    /// <summary>
    /// Cross-origin isolation, which is what lets the in-browser CPU model use more than one
    /// thread: threads need <c>SharedArrayBuffer</c>, and a browser only hands that to an isolated
    /// page (ADR-011). Everything this app loads from elsewhere — model weights, a naming API — it
    /// loads with <c>fetch</c> under CORS, which <c>require-corp</c> already accepts, so nothing
    /// has to be given up for it. <c>require-corp</c> rather than <c>credentialless</c> because
    /// every browser understands it, Safari included.
    /// </summary>
    private static void Isolate(HttpResponse response)
    {
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    }

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
                // Hashed immutable assets vs. everything else (index.html, manifest, service worker).
                bool immutable = ctx.Context.Request.Path.StartsWithSegments("/_app/immutable");
                ctx.Context.Response.Headers.CacheControl = immutable ? "public, max-age=31536000, immutable" : "no-cache";
                Isolate(ctx.Context.Response);
            },
        };

        app.UseDefaultFiles();
        app.UseStaticFiles(options);

        // The same options, because every deep link is served by the fallback rather than by the
        // static middleware: without this, isolation would hold on "/" and silently fail on
        // "/settings", which is the one page that turns the model on.
        app.MapFallbackToFile("index.html", options);
    }
}
