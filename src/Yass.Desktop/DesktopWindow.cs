using System.Diagnostics.CodeAnalysis;
using Photino.NET;

namespace Yass.Desktop;

/// <summary>The Photino window: thin glue between the webview and <see cref="AppScheme"/> and the bridge.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs a display and a native webview; the logic it wires is tested on its own.")]
internal static class DesktopWindow
{
    /// <summary>Opens the window on the calling (main) thread and blocks until it closes.</summary>
    public static void Run(IServiceProvider services, ILogger logger)
    {
        AppScheme scheme = services.GetRequiredService<AppScheme>();
        BridgeNotifier notifier = services.GetRequiredService<BridgeNotifier>();
        IHostApplicationLifetime lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        PhotinoWindow? opened = null;
        var dialogs = new PhotinoDialogs(() => opened);
        var desktop = new DesktopApi(dialogs, services.GetRequiredService<Yass.App.Export.ExportDownloads>(), services.GetRequiredService<ILogger<DesktopApi>>());
        var bridge = new BridgeDispatcher(
            services.GetRequiredService<Yass.App.Api.YassApi>(),
            message => opened?.SendWebMessage(message),
            logger,
            [desktop],
            lifetime.ApplicationStopping);

        var window = new PhotinoWindow()
            .SetTitle("YASS")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 860)
            .SetMinSize(360, 480)
            .Center()
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(Environment.GetEnvironmentVariable("YASS_DEVTOOLS") is "1" or "true")
            .RegisterCustomSchemeHandler(AppScheme.Scheme, (object sender, string _, string url, out string contentType) =>
            {
                try
                {
                    SchemeResponse? response = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                        ? scheme.Resolve(uri, lifetime.ApplicationStopping)
                        : null;
                    logger.LogDebug("{Url} -> {Type}", url, response?.ContentType ?? "nothing");
                    contentType = response?.ContentType ?? "text/plain";
                    return response?.Body ?? new MemoryStream();
                }
#pragma warning disable CA1031 // A failure must not take the window down; the request just comes back empty.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    logger.LogWarning(ex, "Request {Url} failed", url);
                    contentType = "text/plain";
                    return new MemoryStream();
                }
            })
            .RegisterWebMessageReceivedHandler((_, message) => bridge.Receive(message));

        opened = window;
        notifier.Attach(window.SendWebMessage);
        using CancellationTokenRegistration closeOnStop = lifetime.ApplicationStopping.Register(() => window.Invoke(window.Close));

        window.Load(new Uri(AppScheme.Origin + "/"));
        window.WaitForClose();
        notifier.Attach(static _ => { });
    }
}

/// <summary>Photino's dialogs, opened on the window's own thread as every toolkit requires.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs a display and a native webview.")]
internal sealed class PhotinoDialogs(Func<PhotinoWindow?> window) : IDesktopDialogs
{
    public Task<string?> SaveFileAsync(string suggestedPath, string extension) =>
        OnWindowThread(w => w.ShowSaveFile(
            "Save as",
            suggestedPath,
            string.IsNullOrEmpty(extension) ? [] : [(extension.TrimStart('.').ToUpperInvariant(), ["*" + extension])]));

    public Task<string?> PickFolderAsync(string startFolder) =>
        OnWindowThread(w => w.ShowOpenFolder("Save into folder", startFolder, multiSelect: false).FirstOrDefault());

    private Task<string?> OnWindowThread(Func<PhotinoWindow, string?> show)
    {
        PhotinoWindow w = window() ?? throw new InvalidOperationException("The window is not open.");
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        w.Invoke(() =>
        {
            try
            {
                done.SetResult(show(w));
            }
#pragma warning disable CA1031 // Handed to the caller through the task.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                done.SetException(ex);
            }
        });
        return done.Task;
    }
}
