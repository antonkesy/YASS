using Yawss.Core.Export;
using Yawss.Core.Scanning;
using Yawss.Fake;
using Yawss.Scanning;
using Yawss.Server.Config;

namespace Yawss.Server.Hosting;

/// <summary>Chooses fake or real scanner/PDF implementations. The real PDF writer arrives with Yawss.Export.</summary>
public static class BackendRegistration
{
    public static void Add(IServiceCollection services, StartupOptions startup)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(startup);

        if (startup.Fake)
        {
            services.AddSingleton<IScannerBackend>(_ => new FakeScannerBackend(FakeScenario.Parse(startup.FakeScenario)));
            services.AddSingleton<IDocumentWriter, FakeDocumentWriter>();
            services.AddSingleton<IDocumentStore>(sp => new FakeDocumentStore(sp.GetRequiredService<ConfigStore>().Current.Output.Directory));
            return;
        }

        services.AddSingleton<IScannerBackend>(sp =>
        {
            ConfigStore config = sp.GetRequiredService<ConfigStore>();
            return new Naps2ScannerBackend(() => config.Current.Scanner, sp.GetRequiredService<ILogger<Naps2ScannerBackend>>(), sp.GetRequiredService<TimeProvider>());
        });

        // Until Yawss.Export provides the NAPS2 PDF writer, real scans still export through the fake writer.
        services.AddSingleton<IDocumentWriter, FakeDocumentWriter>();
        services.AddSingleton<IDocumentStore>(sp => new FakeDocumentStore(sp.GetRequiredService<ConfigStore>().Current.Output.Directory));
    }
}
