using System.Diagnostics.CodeAnalysis;
using Android.Webkit;
using Yass.Bridge;

namespace Yass.Android;

/// <summary>Answers the app's own origin from <see cref="AppScheme"/>; links anywhere else open in the browser.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs a WebView.")]
internal sealed class AppWebViewClient(AppScheme scheme, ILogger logger, Action<Uri> openExternal, CancellationToken stopping) : WebViewClient
{
    public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request) =>
        SchemeRequests.Answer(scheme, request, logger, stopping);

    public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
    {
        if (!Uri.TryCreate(request?.Url?.ToString(), UriKind.Absolute, out Uri? url) || scheme.Origin.Owns(url))
        {
            return false;
        }

        if (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
        {
            try
            {
                openExternal(url);
            }
            catch (BridgeException ex)
            {
                logger.LogWarning(ex, "Could not open {Url}", url);
            }
        }

        return true;
    }
}

[ExcludeFromCodeCoverage(Justification = "Needs a WebView.")]
internal sealed class AppServiceWorkerClient(AppScheme scheme, ILogger logger, CancellationToken stopping) : ServiceWorkerClient
{
    public override WebResourceResponse? ShouldInterceptRequest(IWebResourceRequest? request) =>
        SchemeRequests.Answer(scheme, request, logger, stopping);
}

[ExcludeFromCodeCoverage(Justification = "Needs a WebView.")]
internal static class SchemeRequests
{
    /// <summary>Called on a WebView worker thread, which may wait. Null lets the WebView fetch a URL that is not the app's.</summary>
    public static WebResourceResponse? Answer(AppScheme scheme, IWebResourceRequest? request, ILogger logger, CancellationToken stopping)
    {
        string? url = request?.Url?.ToString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || !scheme.Origin.Owns(uri))
        {
            return null;
        }

        try
        {
            SchemeResponse? response = scheme.Resolve(uri, stopping);
            logger.LogDebug("{Url} -> {Type}", url, response?.ContentType ?? "nothing");
            if (response is null)
            {
                // Never null for the app's own host: the WebView would look yass.localhost up on the network.
                return Status(404, "Not Found");
            }

            string[] type = response.ContentType.Split(';', 2, StringSplitOptions.TrimEntries);
            string? charset = type.Length > 1 && type[1].StartsWith("charset=", StringComparison.OrdinalIgnoreCase) ? type[1]["charset=".Length..] : null;
            return new WebResourceResponse(type[0], charset, response.Body);
        }
#pragma warning disable CA1031 // A failure must not take the app down; the request just fails.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Request {Url} failed", url);
            return Status(500, "Internal Server Error");
        }
    }

    private static WebResourceResponse Status(int code, string reason) =>
        new("text/plain", "utf-8", code, reason, new Dictionary<string, string>(), new MemoryStream());
}
