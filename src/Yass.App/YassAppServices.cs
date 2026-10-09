using Yass.App.Api;
using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Hosting;
using Yass.App.Rendering;
using Yass.App.Scanning;
using Yass.App.Sessions;
using Yass.Core.Config;

namespace Yass.App;

public static class YassAppServices
{
    /// <summary>
    /// Registers the whole app: config, scanner backend, sessions, rendering, export, the hosted
    /// workers and <see cref="YassApi"/>. The host adds its <see cref="IClientNotifier"/> and
    /// <see cref="IDownloadPaths"/>, and whatever transport it carries calls over.
    /// </summary>
    public static IServiceCollection AddYassApp(this IServiceCollection services, YassConfig config, StartupOptions startup)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(startup);

        services.AddSingleton(startup);
        services.AddSingleton(sp => new ConfigStore(config, startup.ConfigPath, sp.GetRequiredService<ILogger<ConfigStore>>()));
        services.AddSingleton(TimeProvider.System);
        BackendRegistration.Add(services, startup);

        services.AddSingleton<EffectiveOptions>();
        services.AddSingleton<ScannerStatusMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<ScannerStatusMonitor>());

        services.AddSingleton<SessionStore>();
        services.AddSingleton<SessionMapper>();
        services.AddSingleton<ImageRenderer>();
        services.AddSingleton<RenderCache>();
        services.AddSingleton<IThumbnailStatus>(sp => sp.GetRequiredService<RenderCache>());
        services.AddSingleton<ImageSource>();
        services.AddSingleton<ThumbnailWorker>();
        services.AddSingleton<IPageSink>(sp => sp.GetRequiredService<ThumbnailWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<ThumbnailWorker>());
        services.AddSingleton<ScanService>();
        services.AddSingleton<EditService>();
        services.AddSingleton<AutoCropService>();
        services.AddSingleton<DownloadTokens>();
        services.AddSingleton<ExportDownloads>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<YassApi>();
        services.AddSingleton<SessionRetentionService>();
        services.AddHostedService(sp => sp.GetRequiredService<SessionRetentionService>());
        return services;
    }
}
