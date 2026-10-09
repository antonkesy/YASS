using Yass.App.Config;
using Yass.Core.Export;
using Yass.Core.Scanning;
using Yass.Export;
using Yass.Fake;
using Yass.Scanning;

namespace Yass.App.Hosting;

/// <summary>Chooses fake or real scanner/PDF implementations. The real PDF writer arrives with Yass.Export.</summary>
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
            return;
        }

        services.AddSingleton<IScannerBackend>(sp =>
        {
            ConfigStore config = sp.GetRequiredService<ConfigStore>();
            return new Naps2ScannerBackend(() => config.Current.Scanner, sp.GetRequiredService<ILogger<Naps2ScannerBackend>>(), sp.GetRequiredService<TimeProvider>());
        });

        services.AddSingleton<IDocumentWriter, Naps2PdfWriter>();
    }
}
