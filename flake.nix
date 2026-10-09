{
  description = "YASS - Yet Another Slop Scanner";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
    }:
    {
      nixosModules.default = import ./nix/module.nix { inherit self; };
      nixosModules.yass = self.nixosModules.default;
    }
    // flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = nixpkgs.legacyPackages.${system};
        dotnet = pkgs.dotnet-sdk_10;
        version = pkgs.lib.trim (builtins.readFile ./version.txt);
        yass = pkgs.callPackage ./nix/package.nix {
          inherit version;
          src = pkgs.lib.cleanSource ./.;
        };
        # Runs one command with what Photino (Yass.Desktop) loads from the dev shell: WebKitGTK,
        # GTK and an EGL driver, plus the schemas GTK's file dialogs read. Scoped to that one
        # command rather than the shell, so nothing else picks up nix's GUI libraries.
        withDesktopLibs = pkgs.writeShellScriptBin "with-desktop-libs" ''
          export LD_LIBRARY_PATH="${
            pkgs.lib.makeLibraryPath (
              with pkgs;
              [
                webkitgtk_4_1
                gtk3
                glib
                libnotify
                mesa
                stdenv.cc.cc.lib
              ]
            )
          }''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
          # Off NixOS the nix EGL loader cannot find the system's driver.
          export __EGL_VENDOR_LIBRARY_DIRS="''${__EGL_VENDOR_LIBRARY_DIRS:-${pkgs.mesa}/share/glvnd/egl_vendor.d}"
          export XDG_DATA_DIRS="${pkgs.gsettings-desktop-schemas}/share/gsettings-schemas/${pkgs.gsettings-desktop-schemas.name}:${pkgs.gtk3}/share/gsettings-schemas/${pkgs.gtk3.name}:''${XDG_DATA_DIRS:-/usr/local/share:/usr/share}"
          exec "$@"
        '';
      in
      {
        formatter = pkgs.nixfmt;

        packages = {
          default = yass;
          web = yass.passthru.web;
          desktop = pkgs.callPackage ./nix/desktop.nix {
            inherit version;
            src = pkgs.lib.cleanSource ./.;
            web = yass.passthru.web;
          };
          docker = pkgs.dockerTools.buildLayeredImage {
            name = "yass";
            tag = version;
            contents = [
              yass
              pkgs.sane-backends
              pkgs.dockerTools.caCertificates
            ];
            extraCommands = "mkdir -p data/sessions etc/yass";
            config = {
              Cmd = [ "${pkgs.lib.getExe yass}" ];
              Env = [
                "YASS_CONFIG=/etc/yass/yass.yaml"
                "SANE_CONFIG_DIR=${pkgs.sane-backends}/etc/sane.d"
                "DOTNET_CLI_TELEMETRY_OPTOUT=1"
              ];
              ExposedPorts."8080/tcp" = { };
              Volumes."/data" = { };
              WorkingDir = "/data";
            };
          };
        };

        devShells.default = pkgs.mkShell {
          packages = with pkgs; [
            dotnet
            nodejs_24
            pnpm
            sane-backends
            pre-commit
            just # justfile: just desktop, just server, just check
            nixfmt
            git
            python3 # scripts/coverage-gate.py
            # scripts/render-fixtures.sh, run by Yass.Fake's build: the fake scanner's pages
            # are rendered from Markdown rather than committed as images. typst ships no fonts
            # of its own, so one has to be on the path or it refuses to lay anything out.
            pandoc
            typst
            poppler-utils
            dejavu_fonts
            # scripts/render-icons.sh and scripts/package-desktop.sh (ADR-016)
            imagemagick
            libicns
            nsis
            rcodesign
            withDesktopLibs # with-desktop-libs dotnet run --project src/Yass.Desktop
          ];

          TYPST_FONT_PATHS = "${pkgs.dejavu_fonts}/share/fonts";
          DOTNET_ROOT = "${dotnet}";
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
          # Worker nodes and the build server outlive the command and hold pipes open.
          MSBUILDDISABLENODEREUSE = "1";
          DOTNET_CLI_USE_MSBUILD_SERVER = "0";
          # NAPS2 dlopens libsane. The desktop app's GUI libraries are not here but in
          # with-desktop-libs: on this path they would load into every program the shell runs,
          # and Playwright's Chromium, built for the host's glibc, cannot take nix's.
          LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath [ pkgs.sane-backends ];
          SANE_CONFIG_DIR = "${pkgs.sane-backends}/etc/sane.d";

          shellHook = ''
            export PATH="$HOME/.dotnet/tools:$PATH"
          '';
        };
      }
    );
}
