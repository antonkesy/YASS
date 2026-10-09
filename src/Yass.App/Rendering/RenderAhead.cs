namespace Yass.App.Rendering;

/// <summary>
/// What the background worker renders before anyone asks. Thumbnails always; previews too when
/// <paramref name="Previews"/> is set, which a host whose image requests block its UI wants (the
/// desktop's webview asks for an image synchronously, on the window's thread).
/// </summary>
public sealed record RenderAhead(bool Previews = false);
