# NixOS module: services.yass
{ self }:
{
  config,
  lib,
  pkgs,
  ...
}:
let
  cfg = config.services.yass;
  yaml = pkgs.formats.yaml { };
  configFile = yaml.generate "yass.yaml" cfg.settings;
in
{
  options.services.yass = {
    enable = lib.mkEnableOption "YASS, the web scanner service (no authentication; LAN only)";

    package = lib.mkOption {
      type = lib.types.package;
      default = self.packages.${pkgs.stdenv.hostPlatform.system}.default;
      defaultText = lib.literalExpression "yass.packages.\${system}.default";
      description = "The YASS package to run.";
    };

    settings = lib.mkOption {
      type = yaml.type;
      default = { };
      example = lib.literalExpression ''
        {
          server.listen = "0.0.0.0:8080";
          scanner.device = "epsonds";
        }
      '';
      description = ''
        Contents of yass.yaml (see yass.example.yaml). `sessions.directory` defaults to the state directory.
      '';
    };

    fake = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Run with the simulated scanner instead of NAPS2 (for trying the UI without hardware).";
    };

    user = lib.mkOption {
      type = lib.types.str;
      default = "yass";
      description = "User the service runs as. Needs access to the scanner (typically the `scanner` and `lp` groups).";
    };

    group = lib.mkOption {
      type = lib.types.str;
      default = "yass";
      description = "Group the service runs as.";
    };

    openFirewall = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Open the configured port. There is no authentication: only do this on a trusted LAN.";
    };
  };

  config = lib.mkIf cfg.enable (
    let
      listen = cfg.settings.server.listen or "0.0.0.0:8080";
      port = lib.toInt (lib.last (lib.splitString ":" listen));
      settings = lib.recursiveUpdate { sessions.directory = "/var/lib/yass/sessions"; } cfg.settings;
      configFile' = yaml.generate "yass.yaml" settings;
    in
    {
      users.users = lib.mkIf (cfg.user == "yass") {
        yass = {
          isSystemUser = true;
          group = cfg.group;
          extraGroups = [
            "scanner"
            "lp"
          ];
          home = "/var/lib/yass";
        };
      };
      users.groups = lib.mkIf (cfg.group == "yass") { yass = { }; };

      systemd.services.yass = {
        description = "YASS web scanner service";
        wantedBy = [ "multi-user.target" ];
        after = [ "network.target" ];
        environment = {
          YASS_CONFIG = configFile';
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        }
        // lib.optionalAttrs config.hardware.sane.enable {
          SANE_CONFIG_DIR = "/etc/sane.d";
          LD_LIBRARY_PATH = lib.makeLibraryPath [ pkgs.sane-backends ];
        };
        serviceConfig = {
          ExecStart = "${lib.getExe cfg.package}" + lib.optionalString cfg.fake " --fake";
          User = cfg.user;
          Group = cfg.group;
          StateDirectory = "yass";
          WorkingDirectory = "/var/lib/yass";
          Restart = "on-failure";
          RestartSec = 5;
          # Hardening; USB scanners need /dev/bus/usb, so no PrivateDevices.
          NoNewPrivileges = true;
          ProtectSystem = "strict";
          ProtectHome = true;
          PrivateTmp = true;
          ReadWritePaths = [
            "/var/lib/yass"
          ];
        };
      };

      networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [ port ];

      warnings = lib.optional cfg.openFirewall "services.yass.openFirewall exposes an unauthenticated service; keep it on a trusted LAN.";
    }
  );
}
