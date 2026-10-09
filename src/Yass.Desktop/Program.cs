using Yass.App;
using Yass.App.Api;
using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Hosting;
using Yass.App.Rendering;
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
        // A background service that fails (the scanner probe, say) is logged and the window stays
        // open; the host's default would stop the app, and with it close the window.
        builder.Services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        var notifier = new BridgeNotifier();
        string sessions = DesktopDefaults.SessionsDirectory();
        builder.Services.AddYassApp(config, startup, c => DesktopDefaults.Apply(c, sessions, Environment.ProcessorCount));
        // The webview asks for an image on its own thread and waits; render previews before it asks.
        builder.Services.AddSingleton(new RenderAhead(Previews: true));
        builder.Services.AddSingleton(notifier);
        builder.Services.AddSingleton<IClientNotifier>(notifier);
        builder.Services.AddSingleton<IDownloadPaths>(new DownloadPaths());
        builder.Services.AddSingleton(sp => new AppScheme(WebRoot.Resolve(), sp.GetRequiredService<ImageSource>(), sp.GetRequiredService<ExportDownloads>()));

        using IHost host = builder.Build();
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yass.Desktop");
        if (File.Exists(startup.ConfigPath))
        {
            logger.LogInformation("Config loaded from {Path}", Path.GetFullPath(startup.ConfigPath));
        }
        else
        {
            logger.LogInformation("Config file {Path} not found; running with built-in defaults", Path.GetFullPath(startup.ConfigPath));
        }

        if (startup.Fake)
        {
            logger.LogInformation("Fake mode: scanner and PDF writer are simulated (scenario: {Scenario})", startup.FakeScenario ?? "default");
        }

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
