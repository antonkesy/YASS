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
    /// <summary><paramref name="hostDefaults"/> runs on <paramref name="config"/> here and again on every reload.</summary>
    public static IServiceCollection AddYassApp(this IServiceCollection services, YassConfig config, StartupOptions startup, Func<YassConfig, YassConfig>? hostDefaults = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(startup);

        services.AddSingleton(startup);
        YassConfig initial = hostDefaults?.Invoke(config) ?? config;
        services.AddSingleton(sp => new ConfigStore(initial, startup.ConfigPath, sp.GetRequiredService<ILogger<ConfigStore>>(), hostDefaults));
        services.AddSingleton(TimeProvider.System);
        BackendRegistration.Add(services, startup);

        services.AddSingleton<EffectiveOptions>();
        services.AddSingleton<ScannerStatusMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<ScannerStatusMonitor>());

        services.AddSingleton<SessionStore>();
        services.AddSingleton<SessionMapper>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<ImageRenderer>();
        services.AddSingleton<RenderCache>();
        services.AddSingleton<IThumbnailStatus>(sp => sp.GetRequiredService<RenderCache>());
        services.AddSingleton<ImageSource>();
        // A host that wants more rendered ahead registers its own after this one.
        services.AddSingleton(new RenderAhead());
        services.AddSingleton<ThumbnailWorker>();
        services.AddSingleton<IPageSink>(sp => sp.GetRequiredService<ThumbnailWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<ThumbnailWorker>());
        services.AddSingleton<ScanService>();
        services.AddSingleton<EditService>();
        services.AddSingleton<AutoCropService>();
        services.AddSingleton<MergeService>();
        services.AddSingleton<DownloadTokens>();
        services.AddSingleton<ExportDownloads>();
        services.AddSingleton<ExportDocumentWriter>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<YassApi>();
        services.AddSingleton<SessionRetentionService>();
        services.AddHostedService(sp => sp.GetRequiredService<SessionRetentionService>());
        return services;
    }
}
