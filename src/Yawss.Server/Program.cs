using MessagePack;
using MessagePack.Resolvers;
using Yawss.Core.Config;
using Yawss.Server.Config;
using Yawss.Server.Hosting;
using Yawss.Server.Hub;
using Yawss.Server.Scanning;

StartupOptions startup = StartupOptions.Parse(args);

YawssConfig config;
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

builder.Services
    .AddSignalR(o => o.EnableDetailedErrors = true)
    .AddMessagePackProtocol(o => o.SerializerOptions = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithSecurity(MessagePackSecurity.UntrustedData));

WebApplication app = builder.Build();

app.Logger.LogWarning("YAWSS has no authentication. Listening on {Listen}; keep it on a trusted LAN.", config.Server.Listen);
if (startup.Fake)
{
    app.Logger.LogInformation("Fake mode: scanner and PDF writer are simulated (scenario: {Scenario})", startup.FakeScenario ?? "default");
}

if (config.Server.BasePath != "/")
{
    app.UsePathBase(config.Server.BasePath.TrimEnd('/'));
}

app.MapGet("/healthz", () => Results.Text("ok"));
app.MapHub<YawssHub>("/hub");

StaticFrontend.Map(app);

app.Run();
return 0;

/// <summary>Entry point marker for <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}
