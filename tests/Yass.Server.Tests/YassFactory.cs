using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Scanning;
using Yass.Fake;
using Yass.Server.Config;

namespace Yass.Server.Tests;

/// <summary>
/// Boots the real server against a fake scanner and temp directories. Each instance is isolated, so test classes
/// can run in parallel with different scenarios.
/// </summary>
public sealed class YassFactory : WebApplicationFactory<Program>
{
    public YassFactory(FakeScenario? scenario = null, Func<YassConfig, YassConfig>? configure = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "yass-tests", Guid.NewGuid().ToString("N"));
        Scenario = scenario ?? FakeScenario.Pages(3, 2);
        YassConfig config = new YassConfig() with
        {
            Sessions = new SessionsConfig { Directory = Path.Combine(Root, "sessions") },
            Output = new OutputConfig { Directory = Path.Combine(Root, "consume") },
            Scanner = new ScannerConfig { PollIntervalSeconds = 1 },
        };
        Config = configure?.Invoke(config) ?? config;
        Backend = new FakeScannerBackend(Scenario);
    }

    public string Root { get; }

    public FakeScenario Scenario { get; }

    public YassConfig Config { get; }

    public FakeScannerBackend Backend { get; }

    public string ConsumeDirectory => Config.Output.Directory;

    public HubConnection CreateHub()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hub"), o => o.HttpMessageHandlerFactory = _ => Server.CreateHandler())
            .AddMessagePackProtocol(o => o.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance)
                .WithSecurity(MessagePackSecurity.UntrustedData))
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(sp => new ConfigStore(Config, Path.Combine(Root, "yass.yaml"), sp.GetRequiredService<ILogger<ConfigStore>>()));
            services.AddSingleton<IScannerBackend>(Backend);
            services.AddSingleton<IDocumentWriter, FakeDocumentWriter>();
            services.AddSingleton<IDocumentStore>(_ => new FakeDocumentStore(ConsumeDirectory));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
