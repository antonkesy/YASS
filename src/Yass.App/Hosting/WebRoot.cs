namespace Yass.App.Hosting;

public static class WebRoot
{
    public const string Env = "YASS_WEBROOT";

    /// <summary><c>YASS_WEBROOT</c>, else <c>wwwroot</c> next to the binary (publish/Docker), else in the working directory.</summary>
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
