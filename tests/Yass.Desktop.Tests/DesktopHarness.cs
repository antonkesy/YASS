using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yass.App.Api;
using Yass.App.Hosting;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;
using Yass.Core.Scanning;
using Yass.Fake;

namespace Yass.Desktop.Tests;

/// <summary>
/// The desktop host's services on a Generic Host, wired by the same <see cref="DesktopServices"/> as <c>Program</c>, against the fake
/// scanner and a temp directory. No window: what the window would receive is in <see cref="Sent"/>.
/// </summary>
public sealed class DesktopHarness : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly List<string> _sent = [];

    public DesktopHarness(FakeScenario? scenario = null, AppOrigin? origin = null)
    {
        Origin = origin ?? AppOrigin.Custom;
        Root = Path.Combine(Path.GetTempPath(), "yass-desktop-tests", Guid.NewGuid().ToString("N"));
        WebRoot = Path.Combine(Root, "web");
        Directory.CreateDirectory(Path.Combine(WebRoot, "_app"));
        File.WriteAllText(Path.Combine(WebRoot, "index.html"), "<!doctype html><title>YASS</title>");
        File.WriteAllText(Path.Combine(WebRoot, "_app", "start.js"), "export {};");

        YassConfig config = new YassConfig() with
        {
            Sessions = new SessionsConfig { Directory = Path.Combine(Root, "sessions") },
            Scanner = new ScannerConfig { PollIntervalSeconds = 1 },
        };

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = Root });
        builder.Logging.ClearProviders();
        Notifier = new BridgeNotifier();
        Notifier.Attach(Record);
        StartupOptions startup = new(Path.Combine(Root, "yass.yaml"), Fake: true, FakeScenario: null);
        builder.Services.AddYassDesktop(config, startup, Notifier, WebRoot, Origin, config.Sessions.Directory);
        builder.Services.AddSingleton<IScannerBackend>(new FakeScannerBackend(scenario ?? FakeScenario.Pages(3, 2)));
        builder.Services.AddSingleton<IDocumentWriter, FakeDocumentWriter>();
        _host = builder.Build();
        _host.Start();
    }

    public string Root { get; }

    public AppOrigin Origin { get; }

    public string WebRoot { get; }

    public BridgeNotifier Notifier { get; }

    public IServiceProvider Services => _host.Services;

    public YassApi Api => Services.GetRequiredService<YassApi>();

    public AppScheme Scheme => Services.GetRequiredService<AppScheme>();

    /// <summary>Every message posted to the window so far.</summary>
    public IReadOnlyList<string> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public void Record(string message)
    {
        lock (_sent)
        {
            _sent.Add(message);
        }
    }

    /// <summary>Scans with the fake backend and waits until the session is in review with every thumbnail rendered.</summary>
    public async Task<SessionDto> ScanAsync()
    {
        SessionDto started = await Api.StartScan(null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            SessionDto session = await Api.GetSession(started.Id);
            if (session.State == SessionState.Review && session.Pages.All(p => p.ThumbnailReady))
            {
                return session;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A render finishing after stop may still hold a file; the temp folder is not worth failing over.
        }
    }
}
