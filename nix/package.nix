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

  # The csproj enables PublishTrimmed for the container build; Nix ships the shared runtime instead.
  selfContainedBuild = false;
  dotnetPublishFlags = [ "-p:PublishTrimmed=false" ];

  executables = [ "Yass.Server" ];
  # NAPS2 dlopens libsane.
  runtimeDeps = [ sane-backends ];

  postInstall = ''
    cp -r ${web} $out/lib/yass/wwwroot
    install -Dm644 yass.example.yaml $out/share/yass/yass.example.yaml
  '';

  passthru = {
    inherit web;
  };

  meta = {
    description = "Yet Another Scanner Service: headless scan-to-PDF with a mobile-first web UI";
    homepage = "https://github.com/antonkesy/yass";
    license = lib.licenses.mit;
    mainProgram = "Yass.Server";
    platforms = lib.platforms.linux;
  };
}
