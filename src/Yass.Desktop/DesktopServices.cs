using Yass.App;
using Yass.App.Api;
using Yass.App.Export;
using Yass.App.Hosting;
using Yass.App.Rendering;
using Yass.Bridge;
using Yass.Core.Config;

namespace Yass.Desktop;

public static class DesktopServices
{
    public static IServiceCollection AddYassDesktop(
        this IServiceCollection services,
        YassConfig config,
        StartupOptions startup,
        BridgeNotifier notifier,
        string webRoot,
        AppOrigin origin,
        string sessionsDirectory)
    {
        // The host's default would stop the app on a failed background service, closing the window.
        services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        services.AddYassApp(config, startup, c => DesktopDefaults.Apply(c, sessionsDirectory, Environment.ProcessorCount));
        // The webview asks for an image on its own thread and waits; render previews before it asks.
        services.AddSingleton(new RenderAhead(Previews: true));
        services.AddSingleton(notifier);
        services.AddSingleton<IClientNotifier>(notifier);
        services.AddSingleton<IDownloadPaths>(new DownloadPaths());
        services.AddSingleton(sp => new AppScheme(new DiskStaticFiles(webRoot), sp.GetRequiredService<ImageSource>(), sp.GetRequiredService<ExportDownloads>(), origin));
        return services;
    }
}
