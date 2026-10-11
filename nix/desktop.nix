{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  wrapGAppsHook3,
  webkitgtk_4_1,
  gtk3,
  glib,
  libnotify,
  sane-backends,
  pandoc,
  typst,
  poppler-utils,
  dejavu_fonts,
  version,
  src,
  web,
}:
buildDotnetModule {
  pname = "yass-desktop";
  inherit version src;

  projectFile = "src/Yass.Desktop/Yass.Desktop.csproj";
  nugetDeps = ./deps-desktop.json;

  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  # The desktop host needs no ASP.NET Core (SPEC.md §4).
  dotnet-runtime = dotnetCorePackages.runtime_10_0;

  # Yass.Fake renders the pages its scanner feeds from Markdown as part of the build.
  nativeBuildInputs = [
    pandoc
    typst
    poppler-utils
    wrapGAppsHook3
  ];
  buildInputs = [
    gtk3
    webkitgtk_4_1
  ];
  TYPST_FONT_PATHS = "${dejavu_fonts}/share/fonts";

  selfContainedBuild = false;

  executables = [ "Yass.Desktop" ];
  # Photino dlopens WebKitGTK and GTK; NAPS2 dlopens libsane.
  runtimeDeps = [
    webkitgtk_4_1
    gtk3
    glib
    libnotify
    sane-backends
  ];

  # One wrapper, not two: the GTK schema and module paths go into the dotnet wrapper.
  dontWrapGApps = true;
  preFixup = ''
    makeWrapperArgs+=("''${gappsWrapperArgs[@]}")
  '';

  postInstall = ''
    rm -rf $out/lib/yass-desktop/wwwroot
    cp -r ${web} $out/lib/yass-desktop/wwwroot
    install -Dm644 yass.example.yaml $out/share/yass/yass.example.yaml
  '';

  meta = {
    description = "Yet Another Slop Scanner as a desktop app: scan, review and save, with no network listener";
    homepage = "https://github.com/antonkesy/YASS";
    license = lib.licenses.mit;
    mainProgram = "Yass.Desktop";
    platforms = lib.platforms.linux;
  };
}
