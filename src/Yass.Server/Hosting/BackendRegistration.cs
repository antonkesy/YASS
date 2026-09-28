using Yass.Core.Export;
using Yass.Core.Naming;
using Yass.Core.Scanning;
using Yass.Export;
using Yass.Fake;
using Yass.Naming;
using Yass.Scanning;
using Yass.Server.Config;

namespace Yass.Server.Hosting;

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
            services.AddSingleton<IDocumentStore>(sp => new FakeDocumentStore(sp.GetRequiredService<ConfigStore>().Current.Output.Directory));
            return;
        }

        services.AddSingleton<IScannerBackend>(sp =>
        {
            ConfigStore config = sp.GetRequiredService<ConfigStore>();
            return new Naps2ScannerBackend(() => config.Current.Scanner, sp.GetRequiredService<ILogger<Naps2ScannerBackend>>(), sp.GetRequiredService<TimeProvider>());
        });

        services.AddSingleton<IDocumentWriter, Naps2PdfWriter>();
        services.AddSingleton<IDocumentStore>(sp =>
        {
            ConfigStore config = sp.GetRequiredService<ConfigStore>();
            return new ConsumeDirectoryStore(() => config.Current.Output, sp.GetRequiredService<ILogger<ConsumeDirectoryStore>>());
        });
    }

    /// <summary>
    /// Name suggestion, which is off unless configured. Registered for both real and fake
    /// backends: reading text off a fake page is exactly as meaningful a test as reading a
    /// real one, and the suggester degrades to the template when it cannot read.
    /// </summary>
    public static void AddNaming(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<TesseractTextReader>();
        services.AddHttpClient<AiNamer>();
        services.AddSingleton<INameSuggester>(sp =>
        {
            ConfigStore config = sp.GetRequiredService<ConfigStore>();
            return new NameSuggester(
                sp.GetRequiredService<TesseractTextReader>(),
                sp.GetRequiredService<AiNamer>(),
                () => config.Current.Naming,
                sp.GetRequiredService<ILogger<NameSuggester>>());
        });
    }
}
