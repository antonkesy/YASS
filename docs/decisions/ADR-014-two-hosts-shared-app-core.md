# ADR-014: Two hosts, server and desktop, over one host-agnostic app core

- **Status:** accepted
- **Date:** 2026-10-09

## Context

YASS ships as a LAN server that browsers use. It is also wanted as a desktop app on Linux,
Windows and macOS that opens no listening socket and is as fast as it can be. The app logic
lived in `Yass.Server`. Six services pushed events through `IHubContext<YassHub, IYassClient>`,
the hub held domain logic of its own (discard, merge, clear, rename), and the export URL was
written as a literal string. None of that could run without Kestrel and SignalR.

## Decision

- **`Yass.App`** holds everything that does not depend on a transport: config, sessions, the
  scan loop, rendering, export, the status monitor and retention.
  - `YassApi : IYassHub` takes over the logic that used to live in the hub.
  - `IClientNotifier` exposes `IYassClient All` and is the only way events go out.
  - `ImageSource` and `ExportDownloads` resolve what the `/img` and `/export` paths point to.
  - `IDownloadPaths` is the host's own spelling of a download URL.
  - `AddYassApp(...)` registers all of it.
  - It references `Microsoft.Extensions.*` (hosting, logging, DI) through the
    `Microsoft.AspNetCore.App` framework reference the server already has, and no new NuGet package.
  - It must not use ASP.NET Core or SignalR types.
- **`Yass.Server`** is the server host: Kestrel, the SignalR hub delegating to `YassApi`, a
  `SignalRNotifier`, and HTTP endpoints that add only ETag, cache headers and status codes.
- **`Yass.Desktop`** is the desktop host (ADR-015). Hub calls go over a JSON message bridge to
  the same `YassApi`, and a `BridgeNotifier` pushes events back. `yass://` serves the same paths.
- **The frontend is one build.** `getHub()` picks the SignalR connection or the bridge
  connection at runtime. Both implement the existing `ConnectionLike` seam, so components
  do not know which one they have.

## Consequences

- One code path for every scan, edit and export, whichever host runs it. The existing
  `Yass.Server.Tests` cover `YassApi` through SignalR, and `Yass.Desktop.Tests` cover it through
  the bridge.
- A new hub method has to be added in three places: `IYassHub`, `YassApi` and the hub's
  one-line delegation. The bridge dispatches by reflection over `IYassHub`, so it needs nothing.
- The export download path now honours `server.base_path`. Before, it was always root-absolute.
