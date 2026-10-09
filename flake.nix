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
          ];

          TYPST_FONT_PATHS = "${pkgs.dejavu_fonts}/share/fonts";
          DOTNET_ROOT = "${dotnet}";
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
          # Worker nodes and the build server outlive the command and hold pipes open.
          MSBUILDDISABLENODEREUSE = "1";
          DOTNET_CLI_USE_MSBUILD_SERVER = "0";
          # NAPS2 dlopens libsane; Photino (Yass.Desktop) dlopens WebKitGTK and GTK.
          LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath (
            with pkgs;
            [
              sane-backends
              webkitgtk_4_1
              gtk3
              glib
              libnotify
              mesa
              stdenv.cc.cc.lib
            ]
          );
          SANE_CONFIG_DIR = "${pkgs.sane-backends}/etc/sane.d";

          shellHook = ''
            export PATH="$HOME/.dotnet/tools:$PATH"
            # WebKitGTK needs an EGL driver; off NixOS the nix one cannot find the system's.
            export __EGL_VENDOR_LIBRARY_DIRS="''${__EGL_VENDOR_LIBRARY_DIRS:-${pkgs.mesa}/share/glvnd/egl_vendor.d}"
            # GTK's file dialogs (Yass.Desktop's save and folder pickers) read these schemas.
            export XDG_DATA_DIRS="${pkgs.gsettings-desktop-schemas}/share/gsettings-schemas/${pkgs.gsettings-desktop-schemas.name}:${pkgs.gtk3}/share/gsettings-schemas/${pkgs.gtk3.name}:''${XDG_DATA_DIRS:-/usr/local/share:/usr/share}"
          '';
        };
      }
    );
}
