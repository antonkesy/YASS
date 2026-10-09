# ADR-015: Photino.NET as the desktop shell

- **Status:** accepted
- **Date:** 2026-10-09

## Context

The desktop app (ADR-014) has to run the existing Svelte UI on Linux, Windows and macOS,
open no listening socket, and stay a single .NET process.

## Decision

**`Photino.NET` 4.0.x** (which pulls in `Photino.Native`). It wraps the platform webview:
WebKitGTK 4.1 on Linux, WebView2 on Windows and WKWebView on macOS. It provides the
following in-process:

- a custom URL scheme handler, which serves `yass://`
- a string message channel, `window.external.sendMessage` / `receiveMessage`
- native open-folder and save-file dialogs

Rejected:

- **Tauri** adds a Rust toolchain, and a second process that would have to talk to .NET over a pipe.
- **Electron / Electron.NET** bundles Chromium (about 150 MB) and Node.
- **Avalonia** would mean a second UI to write and keep in step with the Svelte one.
- **MAUI Blazor Hybrid** has no Linux support.
- **Embedded Kestrel on loopback** breaks the no-socket requirement.

## Consequences

- Runtime dependencies:
  - Linux needs `webkit2gtk-4.1`, `gtk3` and `libnotify`, which go in the Nix package's `runtimeDeps`.
  - Windows needs the WebView2 runtime, which ships with Windows 10 and 11.
  - macOS needs nothing extra.
- Each platform engine behaves slightly differently. WebKitGTK and WKWebView have no
  `showSaveFilePicker`, so the desktop saves through the native dialog and a host-side file move.
- Photino.NET's last release was January 2025. If it stalls further, the bridge and scheme
  handler are small enough to move to another webview wrapper; the frontend does not change.

## Amended 2026-10-10: Windows loads the app from `https://yass.localhost`

On Windows the window stayed black. Photino 4.0 registers no custom scheme with WebView2's
environment: it only catches every request through a `*` `WebResourceRequested` filter and passes
the ones whose scheme it knows to the handler. WebView2 will not navigate to an unregistered
scheme, so `yass://app/` never loads.

On Windows the app is therefore `https://yass.localhost/`. `https` is registered as the handler's
scheme, and the handler answers that one host from the same `AppScheme`. For any other https URL
it gives no answer, and the webview fetches that itself. Elsewhere the app stays at `yass://app/`:
WebKitGTK and WKWebView do not let an app take over `https`. Nothing listens on a socket in
either case, since WebView2 hands the request to the handler before going to the network.
