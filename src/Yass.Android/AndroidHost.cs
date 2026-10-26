using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Yass.App;
using Yass.App.Api;
using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Hosting;
using Yass.App.Rendering;
using Yass.Bridge;
using Yass.Core.Config;

namespace Yass.Android;

/// <summary>
/// The app's services, started once per process and kept across activities: a rotation or a trip through the
/// file picker must not stop a scan.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Startup glue on Android; DesktopHarness wires the same services under test.")]
internal static class AndroidHost
{
    private static readonly Lock Gate = new();
    private static IHost? _host;

    /// <summary>Throws <see cref="ConfigValidationException"/> for a bad <c>yass.yaml</c>.</summary>
    public static IServiceProvider Start(Context context, string? fakeScenario)
    {
        lock (Gate)
        {
            if (_host is not null)
            {
                return _host.Services;
            }

            string files = context.FilesDir!.AbsolutePath;
            string[] args = fakeScenario is null ? [] : ["--fake", fakeScenario];
            StartupOptions startup = StartupOptions.Parse(args, AndroidDefaults.ConfigPath(files));
            YassConfig config = ConfigLoader.Load(startup.ConfigPath);

            HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                DisableDefaults = true,
                ContentRootPath = files,
            });

            // Console output goes to logcat (tag DOTNET).
            LoggingSetup.Configure(builder.Logging, config.Logging);
            IServiceCollection services = builder.Services;
            // The host's default would stop the app on a failed background service.
            services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
            string sessions = AndroidDefaults.SessionsDirectory(files);
            services.AddYassApp(config, startup, c => AndroidDefaults.Apply(c, sessions));
            // The webview asks for an image on its own thread and waits; render previews before it asks.
            services.AddSingleton(new RenderAhead(Previews: true));
            var notifier = new BridgeNotifier();
            services.AddSingleton(notifier);
            services.AddSingleton<IClientNotifier>(notifier);
            services.AddSingleton<IDownloadPaths>(new DownloadPaths());
            services.AddSingleton(sp => new AppScheme(
                new AssetStaticFiles(context.Assets!),
                sp.GetRequiredService<ImageSource>(),
                sp.GetRequiredService<ExportDownloads>(),
                AppOrigin.Https));

            IHost host = builder.Build();
            ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yass.Android");
            StartupLog.Write(logger, startup, missingConfig: Microsoft.Extensions.Logging.LogLevel.Information);
            host.Start();
            _host = host;
            return host.Services;
        }
    }
}
