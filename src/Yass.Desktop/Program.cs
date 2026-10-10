using Yass.App.Config;
using Yass.App.Hosting;
using Yass.Core.Config;

namespace Yass.Desktop;

/// <summary>
/// The desktop app: the same services as the server on a Generic Host, and the same UI in a
/// native window. Nothing listens on a socket; the window talks to the host in-process.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Startup glue around a native window; DesktopHarness wires the same services under test.")]
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        DesktopLog.RedirectWhenWindowless();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => DesktopLog.Fatal(e.ExceptionObject.ToString() ?? "Unknown error");
        try
        {
            return Run(args);
        }
#pragma warning disable CA1031 // The last stop: say what went wrong rather than vanish.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            DesktopLog.Fatal(ex.ToString());
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        StartupOptions startup = StartupOptions.Parse(args, DesktopDefaults.ConfigPath());

        YassConfig config;
        try
        {
            config = ConfigLoader.Load(startup.ConfigPath);
        }
        catch (ConfigValidationException ex)
        {
            DesktopLog.Fatal($"{startup.ConfigPath}: {ex.Message}");
            return 2;
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        LoggingSetup.Configure(builder.Logging, config.Logging);
        builder.Services.AddYassDesktop(config, startup, new BridgeNotifier(), WebRoot.Resolve(), AppOrigin.ForThisPlatform(), DesktopDefaults.SessionsDirectory());

        using IHost host = builder.Build();
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yass.Desktop");
        StartupLog.Write(logger, startup, missingConfig: Microsoft.Extensions.Logging.LogLevel.Information);

        host.Start();
        try
        {
            DesktopWindow.Run(host.Services, logger);
            logger.LogInformation("Window closed");
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }

        return 0;
    }
}
