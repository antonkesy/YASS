namespace Yass.App.Rendering;

// Previews: for a host whose image requests block its UI (the desktop webview asks synchronously, on the window thread).
public sealed record RenderAhead(bool Previews = false);
