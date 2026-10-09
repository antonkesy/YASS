using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.SignalR;
using Yass.App;
using Yass.App.Api;
using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Hosting;
using Yass.Core.Config;
using Yass.Server.Export;
using Yass.Server.Hosting;
using Yass.Server.Hub;
using Yass.Server.Rendering;

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
    WebRootPath = WebRoot.Resolve(),
});

LoggingSetup.Configure(builder.Logging, config.Logging);
builder.WebHost.UseUrls("http://" + config.Server.Listen);

builder.Services.AddYassApp(config, startup);
builder.Services.AddSingleton<IClientNotifier, SignalRNotifier>();
builder.Services.AddSingleton<IDownloadPaths>(new DownloadPaths(config.Server.BasePath));

builder.Services
    // On: SignalR prefixes every hub error with "An unexpected error occurred invoking
    // 'X' on the server. <Type>: " regardless, so turning this off would lose the detail
    // without cleaning up the message. The client strips the prefix (see hubError).
    .AddSignalR(o =>
    {
        o.EnableDetailedErrors = true;
        o.AddFilter<YassErrorFilter>();
    })
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
