namespace Yass.App.Hosting;

/// <summary>Where the SvelteKit build is, for whichever host serves it.</summary>
public static class WebRoot
{
    public const string Env = "YASS_WEBROOT";

    /// <summary>
    /// <c>YASS_WEBROOT</c> if set, else <c>wwwroot</c> next to the binary (publish/Docker layout), else
    /// <c>wwwroot</c> in the working directory (<c>dotnet run</c>).
    /// </summary>
    public static string Resolve()
    {
        string? fromEnv = Environment.GetEnvironmentVariable(Env);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        string beside = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        return Directory.Exists(beside) ? beside : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }
}
