# YASS server + frontend as one Nix package: $out/bin/Yass.Server with the SvelteKit build in wwwroot.
{
  lib,
  stdenv,
  buildDotnetModule,
  dotnetCorePackages,
  fetchPnpmDeps,
  pnpmConfigHook,
  pnpm,
  nodejs_24,
  sane-backends,
  pandoc,
  typst,
  poppler-utils,
  dejavu_fonts,
  version,
  src,
}:
let
  web = stdenv.mkDerivation (finalAttrs: {
    pname = "yass-web";
    inherit version;
    src = src + "/web";

    nativeBuildInputs = [
      nodejs_24
      pnpm
      pnpmConfigHook
    ];

    pnpmDeps = fetchPnpmDeps {
      inherit (finalAttrs) pname version src;
      inherit pnpm;
      fetcherVersion = 4;
      hash = "sha256-CLuVn/HU+BfQ+jUzWQxui9GJgiz64vOMKdQJbiOoCic=";
    };

    buildPhase = ''
      runHook preBuild
      pnpm build
      runHook postBuild
    '';

    installPhase = ''
      runHook preInstall
      cp -r build $out
      runHook postInstall
    '';
  });
in
buildDotnetModule {
  pname = "yass";
  inherit version src;

  projectFile = "src/Yass.Server/Yass.Server.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.aspnetcore_10_0;

  # Yass.Fake renders the pages its scanner feeds from Markdown as part of the build.
  nativeBuildInputs = [
    pandoc
    typst
    poppler-utils
  ];
  TYPST_FONT_PATHS = "${dejavu_fonts}/share/fonts";

  # The csproj enables PublishTrimmed for the container build; Nix ships the shared runtime instead.
  selfContainedBuild = false;
  dotnetPublishFlags = [ "-p:PublishTrimmed=false" ];

  executables = [ "Yass.Server" ];
  # NAPS2 dlopens libsane.
  runtimeDeps = [ sane-backends ];

  postInstall = ''
    # The publish already creates wwwroot, because src/Yass.Server/wwwroot/.gitkeep is tracked.
    # Copying onto an existing directory would nest the site inside it, so replace it outright.
    rm -rf $out/lib/yass/wwwroot
    cp -r ${web} $out/lib/yass/wwwroot
    install -Dm644 yass.example.yaml $out/share/yass/yass.example.yaml
  '';

  passthru = {
    inherit web;
  };

  meta = {
    description = "Yet Another Slop Scanner: headless scan-to-PDF with a mobile-first web UI";
    homepage = "https://github.com/antonkesy/YASS";
    license = lib.licenses.mit;
    mainProgram = "Yass.Server";
    platforms = lib.platforms.linux;
  };
}
