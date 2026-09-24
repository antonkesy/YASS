# NixOS module: services.yawss
{ self }:
{
  config,
  lib,
  pkgs,
  ...
}:
let
  cfg = config.services.yawss;
  yaml = pkgs.formats.yaml { };
  configFile = yaml.generate "yawss.yaml" cfg.settings;
in
{
  options.services.yawss = {
    enable = lib.mkEnableOption "YAWSS, the web scanner service (no authentication; LAN only)";

    package = lib.mkOption {
      type = lib.types.package;
      default = self.packages.${pkgs.stdenv.hostPlatform.system}.default;
      defaultText = lib.literalExpression "yawss.packages.\${system}.default";
      description = "The YAWSS package to run.";
    };

    settings = lib.mkOption {
      type = yaml.type;
      default = { };
      example = lib.literalExpression ''
        {
          server.listen = "0.0.0.0:8080";
          scanner.device = "epsonds";
          output.directory = "/var/lib/paperless/consume";
        }
      '';
      description = ''
        Contents of yawss.yaml (see yawss.example.yaml). `sessions.directory` defaults to the state directory.
      '';
    };

    fake = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Run with the simulated scanner instead of NAPS2 (for trying the UI without hardware).";
    };

    user = lib.mkOption {
      type = lib.types.str;
      default = "yawss";
      description = "User the service runs as. Needs access to the scanner (typically the `scanner` and `lp` groups).";
    };

    group = lib.mkOption {
      type = lib.types.str;
      default = "yawss";
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
      settings = lib.recursiveUpdate { sessions.directory = "/var/lib/yawss/sessions"; } cfg.settings;
      configFile' = yaml.generate "yawss.yaml" settings;
    in
    {
      users.users = lib.mkIf (cfg.user == "yawss") {
        yawss = {
          isSystemUser = true;
          group = cfg.group;
          extraGroups = [
            "scanner"
            "lp"
          ];
          home = "/var/lib/yawss";
        };
      };
      users.groups = lib.mkIf (cfg.group == "yawss") { yawss = { }; };

      systemd.services.yawss = {
        description = "YAWSS web scanner service";
        wantedBy = [ "multi-user.target" ];
        after = [ "network.target" ];
        environment = {
          YAWSS_CONFIG = configFile';
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
          StateDirectory = "yawss";
          WorkingDirectory = "/var/lib/yawss";
          Restart = "on-failure";
          RestartSec = 5;
          # Hardening; USB scanners need /dev/bus/usb, so no PrivateDevices.
          NoNewPrivileges = true;
          ProtectSystem = "strict";
          ProtectHome = true;
          PrivateTmp = true;
          ReadWritePaths = [
            "/var/lib/yawss"
          ]
          ++ lib.optional (settings ? output.directory) settings.output.directory;
        };
      };

      networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [ port ];

      warnings = lib.optional cfg.openFirewall "services.yawss.openFirewall exposes an unauthenticated service; keep it on a trusted LAN.";
    }
  );
}
