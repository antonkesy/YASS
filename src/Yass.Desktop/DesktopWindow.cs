using System.Diagnostics.CodeAnalysis;
using Photino.NET;
using Yass.Bridge;

namespace Yass.Desktop;

[ExcludeFromCodeCoverage(Justification = "Needs a display and a native webview; the logic it wires is tested on its own.")]
internal static class DesktopWindow
{
    /// <summary>Runs on the calling (main) thread and blocks until the window closes.</summary>
    public static void Run(IServiceProvider services, ILogger logger)
    {
        AppScheme scheme = services.GetRequiredService<AppScheme>();
        BridgeNotifier notifier = services.GetRequiredService<BridgeNotifier>();
        IHostApplicationLifetime lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        PhotinoWindow? opened = null;
        var dialogs = new PhotinoDialogs(() => opened);
        var desktop = new DesktopApi(dialogs, new ShellBrowser(logger), services.GetRequiredService<Yass.App.Export.ExportDownloads>(), services.GetRequiredService<ILogger<DesktopApi>>());
        var bridge = new BridgeDispatcher(
            services.GetRequiredService<Yass.App.Api.YassApi>(),
            message => opened?.SendWebMessage(message),
            logger,
            [desktop],
            lifetime.ApplicationStopping);

        var window = new PhotinoWindow()
            .SetTitle("YASS")
            // macOS ignores this and takes the .app's icon.
            .SetIconFile(WindowIcon())
            // WebView2 keeps its profile here instead of beside the executable, which may not be writable.
            .SetTemporaryFilesPath(DesktopDefaults.WebViewDataDirectory())
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 860)
            .SetMinSize(360, 480)
            .Center()
            .SetMaximized(true)
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(Environment.GetEnvironmentVariable("YASS_DEVTOOLS") is "1" or "true")
            .RegisterCustomSchemeHandler(scheme.Origin.Scheme, (object sender, string _, string url, out string contentType) =>
            {
                try
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || !scheme.Origin.Owns(uri))
                    {
                        // Not the app's own: on Windows any https URL comes through here. No answer lets the webview fetch it.
                        contentType = "";
                        return null!;
                    }

                    SchemeResponse? response = scheme.Resolve(uri, lifetime.ApplicationStopping);
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
        using CancellationTokenRegistration closeOnStop = lifetime.ApplicationStopping.Register(() =>
        {
            logger.LogInformation("The app is stopping; closing the window");
            window.Invoke(window.Close);
        });

        logger.LogInformation("Opening the window ({Origin})", scheme.Origin.Root);
        window.Load(scheme.Origin.Root);
        window.WaitForClose();
        // Nothing more goes to the native window once it is gone, including answers to calls in flight.
        notifier.Attach(static _ => { });
        opened = null;
    }

    // Windows loads a window icon only from an .ico; elsewhere the web build's PNG.
    private static string WindowIcon()
    {
        string icon = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "yass.ico")
            : Path.Combine(Yass.App.Hosting.WebRoot.Resolve(), "icons", "icon-512.png");
        return File.Exists(icon) ? icon : "";
    }
}

/// <summary>Opened on the window's own thread, as every toolkit requires.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs a display and a native webview.")]
internal sealed class PhotinoDialogs(Func<PhotinoWindow?> window) : IDesktopDialogs
{
    public Task<string?> SaveFileAsync(string folder, string name, string extension) =>
        OnWindowThread(w => SaveDialog.Show(OperatingSystem.IsWindows() ? w.WindowHandle : IntPtr.Zero, "Save as", folder, name, extension));

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

[ExcludeFromCodeCoverage(Justification = "Starts the user's browser.")]
internal sealed class ShellBrowser(ILogger logger) : IBrowser
{
    public void Open(Uri url)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(ex, "Could not open {Url} in a browser", url);
            throw new BridgeException($"Could not open a browser for {url}.");
        }
    }
}
