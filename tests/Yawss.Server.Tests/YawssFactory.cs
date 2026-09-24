using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yawss.Core.Config;
using Yawss.Core.Export;
using Yawss.Core.Scanning;
using Yawss.Fake;
using Yawss.Server.Config;

namespace Yawss.Server.Tests;

/// <summary>
/// Boots the real server against a fake scanner and temp directories. Each instance is isolated, so test classes
/// can run in parallel with different scenarios.
/// </summary>
public sealed class YawssFactory : WebApplicationFactory<Program>
{
    public YawssFactory(FakeScenario? scenario = null, Func<YawssConfig, YawssConfig>? configure = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "yawss-tests", Guid.NewGuid().ToString("N"));
        Scenario = scenario ?? FakeScenario.Pages(3, 2);
        YawssConfig config = new YawssConfig() with
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

    public YawssConfig Config { get; }

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
            services.AddSingleton(sp => new ConfigStore(Config, Path.Combine(Root, "yawss.yaml"), sp.GetRequiredService<ILogger<ConfigStore>>()));
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
