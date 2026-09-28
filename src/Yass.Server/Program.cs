using MessagePack;
using MessagePack.Resolvers;
using Yass.Core.Config;
using Yass.Server.Config;
using Yass.Server.Export;
using Yass.Server.Hosting;
using Yass.Server.Hub;
using Yass.Server.Rendering;
using Yass.Server.Scanning;
using Yass.Server.Sessions;

StartupOptions startup = StartupOptions.Parse(args);

YassConfig config;
try
{
    config = ConfigLoader.Load(startup.ConfigPath);
}
catch (ConfigValidationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = StaticFrontend.ResolveWebRoot(),
});

LoggingSetup.Configure(builder.Logging, config.Logging);
builder.WebHost.UseUrls("http://" + config.Server.Listen);

builder.Services.AddSingleton(startup);
builder.Services.AddSingleton(sp => new ConfigStore(config, startup.ConfigPath, sp.GetRequiredService<ILogger<ConfigStore>>()));
builder.Services.AddSingleton(TimeProvider.System);
BackendRegistration.Add(builder.Services, startup);

builder.Services.AddSingleton<EffectiveOptions>();
builder.Services.AddSingleton<ScannerStatusMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScannerStatusMonitor>());

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<SessionMapper>();
builder.Services.AddSingleton<ImageRenderer>();
builder.Services.AddSingleton<RenderCache>();
builder.Services.AddSingleton<IThumbnailStatus>(sp => sp.GetRequiredService<RenderCache>());
builder.Services.AddSingleton<ThumbnailWorker>();
builder.Services.AddSingleton<IPageSink>(sp => sp.GetRequiredService<ThumbnailWorker>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ThumbnailWorker>());
builder.Services.AddSingleton<ScanService>();
builder.Services.AddSingleton<EditService>();
builder.Services.AddSingleton<DownloadTokens>();
builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton<SessionRetentionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SessionRetentionService>());

builder.Services
    .AddSignalR(o => o.EnableDetailedErrors = true)
    .AddMessagePackProtocol(o => o.SerializerOptions = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithSecurity(MessagePackSecurity.UntrustedData));

WebApplication app = builder.Build();

app.Logger.LogWarning("YASS has no authentication. Listening on {Listen}; keep it on a trusted LAN.", config.Server.Listen);
if (!File.Exists(startup.ConfigPath))
{
    app.Logger.LogWarning("Config file {Path} not found; running with built-in defaults", Path.GetFullPath(startup.ConfigPath));
}
else
{
    app.Logger.LogInformation("Config loaded from {Path}", Path.GetFullPath(startup.ConfigPath));
}

if (startup.Fake)
{
    app.Logger.LogInformation("Fake mode: scanner and PDF writer are simulated (scenario: {Scenario})", startup.FakeScenario ?? "default");
}

if (config.Server.BasePath != "/")
{
    app.UsePathBase(config.Server.BasePath.TrimEnd('/'));
}

app.MapGet("/healthz", () => Results.Text("ok"));
app.MapHub<YassHub>("/hub");
ImageEndpoints.Map(app);
ExportEndpoints.Map(app);

StaticFrontend.Map(app);

app.Run();
return 0;

/// <summary>Entry point marker for <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}
