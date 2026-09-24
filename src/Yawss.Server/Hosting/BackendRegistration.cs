using Yawss.Core.Export;
using Yawss.Core.Scanning;
using Yawss.Fake;
using Yawss.Server.Config;

namespace Yawss.Server.Hosting;

/// <summary>Chooses fake or real scanner/PDF implementations. Real ones arrive with Yawss.Scanning and Yawss.Export.</summary>
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

        services.AddSingleton<IScannerBackend, UnavailableScannerBackend>();
    }
}

/// <summary>Placeholder until the NAPS2 adapter exists: always reports an error, never scans.</summary>
internal sealed class UnavailableScannerBackend : IScannerBackend
{
    public Task<ScannerStatus> GetStatusAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ScannerStatus(ScannerState.Error, null, "Real scanner backend not available in this build; start with --fake."));

    public IAsyncEnumerable<ScannedPage> ScanAsync(Yawss.Core.Domain.ScanOptions options, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Real scanner backend not available in this build; start with --fake.");
}
