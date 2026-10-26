set shell := ["bash", "-euo", "pipefail", "-c"]

# Inside `nix develop`, or the dev shell's toolchain on PATH: run as is. Otherwise wrap in it.
dev := if env("IN_NIX_SHELL", "") != "" { "" } else { "nix develop -c" }

# The desktop app against the simulated scanner. Pass a scenario, or "" for a real scanner.
desktop scenario="pages=3,blank=2": web
    YASS_WEBROOT="$PWD/web/build" {{ dev }} with-desktop-libs dotnet run --project src/Yass.Desktop -- {{ if scenario == "" { "" } else { "--fake '" + scenario + "'" } }}

# The server against the simulated scanner, on http://localhost:8080.
server scenario="pages=3,blank=2": web
    YASS_WEBROOT="$PWD/web/build" {{ dev }} dotnet run --project src/Yass.Server -- {{ if scenario == "" { "" } else { "--fake '" + scenario + "'" } }}

# Inside `nix develop .#android` (ANDROID_HOME is a real SDK): run as is. Otherwise wrap in it.
android_dev := if path_exists(env("ANDROID_HOME", "/nonexistent")) == "true" { "" } else { "nix develop .#android -c" }

# The Android APK for one ABI into dist/: android-arm64 for phones, android-x64 for the emulator.
android rid="android-arm64": web
    {{ android_dev }} scripts/publish-android.sh dist {{ rid }}

# Build the web app (installs its packages the first time).
web:
    [ -d web/node_modules ] || {{ dev }} pnpm --dir web install --frozen-lockfile
    {{ dev }} pnpm --dir web build

# The commit gate from AGENTS.md.
check:
    {{ dev }} pre-commit run --all-files
    {{ dev }} dotnet test
    {{ dev }} pnpm --dir web test
