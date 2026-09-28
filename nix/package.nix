# YAWSS server + frontend as one Nix package: $out/bin/Yawss.Server with the SvelteKit build in wwwroot.
{
  lib,
  stdenv,
  buildDotnetModule,
  dotnetCorePackages,
  fetchPnpmDeps,
  pnpmConfigHook,
  pnpm,
  nodejs_22,
  sane-backends,
  version,
  src,
}:
let
  web = stdenv.mkDerivation (finalAttrs: {
    pname = "yawss-web";
    inherit version;
    src = src + "/web";

    nativeBuildInputs = [
      nodejs_22
      pnpm
      pnpmConfigHook
    ];

    pnpmDeps = fetchPnpmDeps {
      inherit (finalAttrs) pname version src;
      inherit pnpm;
      fetcherVersion = 4;
      hash = "sha256-g6JByWmsLMNJtzVExlbddbIZPv2HcuiJAYlkuo05Biw=";
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
  pname = "yawss";
  inherit version src;

  projectFile = "src/Yawss.Server/Yawss.Server.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.aspnetcore_8_0;

  # The csproj enables PublishTrimmed for the container build; Nix ships the shared runtime instead.
  selfContainedBuild = false;
  dotnetPublishFlags = [ "-p:PublishTrimmed=false" ];

  executables = [ "Yawss.Server" ];
  # NAPS2 dlopens libsane.
  runtimeDeps = [ sane-backends ];

  postInstall = ''
    cp -r ${web} $out/lib/yawss/wwwroot
    install -Dm644 yawss.example.yaml $out/share/yawss/yawss.example.yaml
  '';

  passthru = {
    inherit web;
  };

  meta = {
    description = "Yet Another Web Scanner Service: headless scan-to-PDF with a mobile-first web UI";
    homepage = "https://github.com/antonkesy/yawss";
    license = lib.licenses.mit;
    mainProgram = "Yawss.Server";
    platforms = lib.platforms.linux;
  };
}
