using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yass.App.Config;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Scanning;
using Yass.Fake;
using Yass.Server.Hub;

namespace Yass.Server.Tests;

public sealed class YassFactory : WebApplicationFactory<Program>
{
    public YassFactory(FakeScenario? scenario = null, Func<YassConfig, YassConfig>? configure = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "yass-tests", Guid.NewGuid().ToString("N"));
        Scenario = scenario ?? FakeScenario.Pages(3, 2);
        YassConfig config = new YassConfig() with
        {
            Sessions = new SessionsConfig { Directory = Path.Combine(Root, "sessions") },
            Scanner = new ScannerConfig { PollIntervalSeconds = 1 },
        };
        Config = configure?.Invoke(config) ?? config;
        Backend = new FakeScannerBackend(Scenario);
    }

    public string Root { get; }

    public FakeScenario Scenario { get; }

    public YassConfig Config { get; }

    public FakeScannerBackend Backend { get; }

    public HubConnection CreateHub()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hub"), o => o.HttpMessageHandlerFactory = _ => Server.CreateHandler())
            .AddMessagePackProtocol(o => o.SerializerOptions = HubProtocol.Options)
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(sp => new ConfigStore(Config, Path.Combine(Root, "yass.yaml"), sp.GetRequiredService<ILogger<ConfigStore>>()));
            services.AddSingleton<IScannerBackend>(Backend);
            services.AddSingleton<IDocumentWriter, FakeDocumentWriter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        // A scan still running at host stop may write pages after the first delete; a second pass catches them.
        for (int attempt = 0; Directory.Exists(Root); attempt++)
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100);
            }
        }
    }
}
