using Yass.App;
using Yass.App.Api;
using Yass.App.Export;
using Yass.App.Hosting;
using Yass.App.Rendering;
using Yass.Core.Config;

namespace Yass.Desktop;

/// <summary>The desktop host's services: the app, plus the transport that replaces SignalR and HTTP.</summary>
public static class DesktopServices
{
    /// <summary>
    /// Registers the app with the desktop's defaults, the <paramref name="notifier"/> that carries
    /// its events to the webview, and the <c>yass://</c> scheme serving <paramref name="webRoot"/>.
    /// </summary>
    public static IServiceCollection AddYassDesktop(
        this IServiceCollection services,
        YassConfig config,
        StartupOptions startup,
        BridgeNotifier notifier,
        string webRoot,
        AppOrigin origin,
        string sessionsDirectory)
    {
        // A background service that fails (the scanner probe, say) is logged and the window stays
        // open; the host's default would stop the app, and with it close the window.
        services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        services.AddYassApp(config, startup, c => DesktopDefaults.Apply(c, sessionsDirectory, Environment.ProcessorCount));
        // The webview asks for an image on its own thread and waits; render previews before it asks.
        services.AddSingleton(new RenderAhead(Previews: true));
        services.AddSingleton(notifier);
        services.AddSingleton<IClientNotifier>(notifier);
        services.AddSingleton<IDownloadPaths>(new DownloadPaths());
        services.AddSingleton(sp => new AppScheme(webRoot, sp.GetRequiredService<ImageSource>(), sp.GetRequiredService<ExportDownloads>(), origin));
        return services;
    }
}
