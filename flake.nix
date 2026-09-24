{
  description = "YAWSS - Yet Another Web Scanner Service";

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
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = nixpkgs.legacyPackages.${system};
        dotnet = pkgs.dotnet-sdk_8;
      in
      {
        formatter = pkgs.nixfmt;

        devShells.default = pkgs.mkShell {
          packages = with pkgs; [
            dotnet
            nodejs_22
            pnpm
            sane-backends
            pre-commit
            nixfmt
            git
          ];

          DOTNET_ROOT = "${dotnet}";
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
          # Worker nodes and the build server outlive the command and hold pipes open.
          MSBUILDDISABLENODEREUSE = "1";
          DOTNET_CLI_USE_MSBUILD_SERVER = "0";
          # NAPS2 dlopens libsane; make the host backends visible.
          LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath [ pkgs.sane-backends ];
          SANE_CONFIG_DIR = "${pkgs.sane-backends}/etc/sane.d";

          shellHook = ''
            export PATH="$HOME/.dotnet/tools:$PATH"
          '';
        };
      }
    );
}
