using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Window;
using AndroidX.WebKit;
using Yass.App.Api;
using Yass.App.Export;
using Yass.Bridge;
using Yass.Core.Config;

namespace Yass.Android;

/// <summary>
/// One WebView on <c>https://yass.localhost/</c>: its requests are answered by <see cref="AppScheme"/> and its bridge
/// calls arrive through AndroidX WebKit's message listener (<c>window.yassBridge</c>). Nothing listens on a socket.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Needs a device and a WebView; the logic it wires is tested through the desktop host.")]
[Activity(
    // A stable Java name instead of a generated crc64 one, so `adb shell am start` can name it.
    Name = "io.github.antonkesy.yass.MainActivity",
    Label = "YASS",
    MainLauncher = true,
    Icon = "@mipmap/ic_launcher",
    Theme = "@style/Yass",
    LaunchMode = LaunchMode.SingleTask,
    WindowSoftInputMode = SoftInput.AdjustResize,
    // The WebView and the page in it survive a rotation, a resize or a keyboard.
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public sealed class MainActivity : Activity
{
    private const string BridgeName = "yassBridge";

    private readonly Dictionary<int, TaskCompletionSource<Intent?>> _results = [];
    private int _nextRequest;
    private WebView? _web;
    private BridgeNotifier? _notifier;
    private WifiManager.MulticastLock? _multicast;
    private volatile JavaScriptReplyProxy? _page;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        IServiceProvider services;
        try
        {
            string? fake = null;
#if DEBUG
            // adb shell am start -n io.github.antonkesy.yass/.MainActivity -e fake "pages=3,blank=2"
            fake = Intent?.GetStringExtra("fake");
#endif
            services = AndroidHost.Start(this, fake);
        }
        catch (ConfigValidationException ex)
        {
            ShowMessage($"{AndroidDefaults.ConfigPath(FilesDir!.AbsolutePath)}: {ex.Message}");
            return;
        }

        if (!WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener))
        {
            ShowMessage("YASS needs a newer Android System WebView. Update it from the Play Store and open YASS again.");
            return;
        }

        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Yass.Android");
        AppScheme scheme = services.GetRequiredService<AppScheme>();
        CancellationToken stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var api = new AndroidApi(this, services.GetRequiredService<ExportDownloads>(), services.GetRequiredService<ILogger<AndroidApi>>());
        var bridge = new BridgeDispatcher(services.GetRequiredService<YassApi>(), Send, logger, [api], stopping);

        // eSCL scanners are found over mDNS, which Android drops unless an app holds this lock.
        var wifi = (WifiManager?)ApplicationContext!.GetSystemService(WifiService);
        _multicast = wifi?.CreateMulticastLock("yass-escl");
        _multicast?.SetReferenceCounted(false);

#if DEBUG
        WebView.SetWebContentsDebuggingEnabled(true);
#endif
        var web = new WebView(this);
        WebSettings settings = web.Settings;
        settings.JavaScriptEnabled = true;
        // Scan settings, presets and scanner names live in localStorage (SPEC §8).
        settings.DomStorageEnabled = true;
        settings.AllowFileAccess = false;
        settings.AllowContentAccess = false;
        web.SetWebViewClient(new AppWebViewClient(scheme, logger, OpenExternal, stopping));
        // The PWA's service worker fetches the app shell; those requests skip the WebViewClient.
        ServiceWorkerController.Instance.SetServiceWorkerClient(new AppServiceWorkerClient(scheme, logger, stopping));
        WebViewCompat.AddWebMessageListener(web, BridgeName, new HashSet<string> { scheme.Origin.Root.GetLeftPart(UriPartial.Authority) }, new BridgeListener(bridge, page => _page = page));
        SetContentView(InsideSystemBars(web));
        _web = web;

        _notifier = services.GetRequiredService<BridgeNotifier>();
        _notifier.Attach(Send);
        ListenForBack();

        logger.LogInformation("Opening {Origin}", scheme.Origin.Root);
        web.LoadUrl(scheme.Origin.Root.AbsoluteUri);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _multicast?.Acquire();
    }

    protected override void OnPause()
    {
        _multicast?.Release();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        // Nothing more goes to this page, including answers to calls in flight; the services keep running.
        _notifier?.Attach(static _ => { });
        _page = null;
        foreach (TaskCompletionSource<Intent?> waiting in _results.Values)
        {
            waiting.TrySetResult(null);
        }

        _results.Clear();
        _web?.Destroy();
        _web = null;
        base.OnDestroy();
    }

    /// <summary>Starts a system activity (file picker, folder picker) and completes with its result, or null when cancelled.</summary>
    internal Task<Intent?> StartForResultAsync(Intent intent)
    {
        var done = new TaskCompletionSource<Intent?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnUiThread(() =>
        {
            try
            {
                int request = ++_nextRequest;
                _results[request] = done;
                StartActivityForResult(intent, request);
            }
            catch (ActivityNotFoundException ex)
            {
                done.TrySetException(new BridgeException("This device has no app to pick a file or folder with.", ex));
            }
        });
        return done.Task;
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (_results.Remove(requestCode, out TaskCompletionSource<Intent?>? waiting))
        {
            waiting.TrySetResult(resultCode == Result.Ok ? data : null);
        }
    }

    public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent? e)
    {
        // Before Android 13; later versions take Back through ListenForBack.
        if (keyCode == Keycode.Back && _web?.CanGoBack() == true)
        {
            _web.GoBack();
            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

    internal void OpenExternal(Uri url)
    {
        try
        {
            using var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url.AbsoluteUri));
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        }
        catch (ActivityNotFoundException ex)
        {
            throw new BridgeException($"Could not open {url}: no browser is installed.", ex);
        }
    }

    // Answers and events go out in the order they were sent, on the UI thread the reply proxy belongs to.
    private void Send(string message) => RunOnUiThread(() => _page?.PostMessage(message));

    private void ListenForBack()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, new BackCallback(() =>
            {
                if (_web?.CanGoBack() == true)
                {
                    _web.GoBack();
                }
                else
                {
                    Finish();
                }
            }));
        }
    }

    // From Android 15 the window draws under the status and navigation bars, and a WebView ignores its own padding:
    // a frame around it keeps the page out from under them and the keyboard.
    private FrameLayout InsideSystemBars(View content)
    {
        var frame = new FrameLayout(this);
        frame.AddView(content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            frame.SetOnApplyWindowInsetsListener(new InsetsListener());
        }

        return frame;
    }

    private void ShowMessage(string message)
    {
        var text = new TextView(this) { Text = message };
        text.SetPadding(48, 96, 48, 48);
        text.SetTextIsSelectable(true);
        SetContentView(text);
    }

    private sealed class BridgeListener(BridgeDispatcher bridge, Action<JavaScriptReplyProxy> seen) : Java.Lang.Object, WebViewCompat.IWebMessageListener
    {
        public void OnPostMessage(WebView? view, WebMessageCompat? message, global::Android.Net.Uri? sourceOrigin, bool isMainFrame, JavaScriptReplyProxy? replyProxy)
        {
            if (!isMainFrame || message?.Data is not { } data || replyProxy is null)
            {
                return;
            }

            // A reload or navigation brings a new proxy; answers go to the page that is there now.
            seen(replyProxy);
            bridge.Receive(data);
        }
    }

    private sealed class BackCallback(Action back) : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked() => back();
    }

    private sealed class InsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                global::Android.Graphics.Insets bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.Ime() | WindowInsets.Type.DisplayCutout());
                v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }

            return insets;
        }
    }
}
