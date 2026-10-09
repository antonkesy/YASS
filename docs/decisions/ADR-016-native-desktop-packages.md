# ADR-016: Native desktop packages, built on Linux

- **Status:** accepted
- **Date:** 2026-10-09

## Context

A release carried the desktop app as one zipped folder per OS, which a person has to unpack
and then find the executable in. Each OS has its own way to ship an app. The release job runs
on Linux, and its zips are cross-compiled there (ADR-015).

## Decision

`scripts/package-desktop.sh` turns the published folders into one native package per OS, all
on Linux:

| OS | Package | Tool |
|---|---|---|
| Linux | `YASS-<v>-x86_64.AppImage` | **appimagetool** 1.9.1 with the type2 runtime 20251108 |
| Windows | `YASS-<v>-windows-x64-setup.exe` | **NSIS** (`makensis`) |
| macOS | `YASS-<v>-macos-arm64.zip` holding `YASS.app` | **rcodesign** for an ad-hoc signature |

- **appimagetool** and its runtime are not in nixpkgs (`appimageTools` there builds Nix
  wrappers, not AppImages). The script downloads both at pinned versions and checks their
  SHA-256 before use, and runs the tool extract-and-run, since runners have no FUSE.
  WebKitGTK is not bundled: like every WebKitGTK app, the AppImage uses the system's.
- **NSIS** installs for the current user, with no administrator prompt, into
  `%LOCALAPPDATA%\Programs\YASS`. It adds a Start menu entry and an uninstaller. That folder is
  fixed, because install and uninstall clear it whole. The user's scans and settings live
  elsewhere and survive both.
- **rcodesign** signs every Mach-O in the bundle ad hoc. Apple Silicon will not start an
  unsigned arm64 binary, and the .NET SDK only signs on a Mac. The app is not notarised, so
  macOS still asks the user to allow it once.
- **A zip, not a `.dmg`:** Linux has no tool for Apple's compressed disk images, and an
  uncompressed ISO-based one is nearly three times the size (114 MB against 43 MB). A zipped
  `.app` is how unsigned Mac apps usually ship, and Safari unpacks it.
- The icons, `assets/logo/yass.ico` and `yass.icns`, are rendered from `yass-512.png` by
  `scripts/render-icons.sh` (ImageMagick, libicns) and committed beside the PNGs.

The dev shell gains `imagemagick`, `libicns`, `nsis` and `rcodesign`; none of them ship in
the app. The Linux and Windows folders stay on the release as portable zips.

## Consequences

- Linux and Windows users get a file that runs on a double click; Mac users get an app to drag
  into Applications.
- Nothing is signed by a trusted identity. Windows SmartScreen and macOS Gatekeeper warn on
  first launch. Signing and notarisation need certificates and, for Apple, a Mac. They are left
  for later and noted in the README.
- The macOS package has not yet been opened on a Mac, nor the installer run on Windows. The
  AppImage was run on Arch Linux.
