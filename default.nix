{
  sources ? import ./nix,
  system ? builtins.currentSystem,
  pkgs ? import sources.nixpkgs {
    inherit system;
    config = { };
    overlays = [ ];
  },
}:
let
  pname = "Phinn";
  version =
    let
      clean =
        str:
        pkgs.lib.pipe str [
          (pkgs.lib.removePrefix "v")
          (pkgs.lib.removeSuffix "\n")
        ];
      version = builtins.readFile ./.version;
    in
    clean version;
  dotnet-sdk = pkgs.dotnet-sdk_10;
  dotnet-runtime = pkgs.dotnet-aspnetcore_10;
in
rec {
  shell = pkgs.mkShell {
    name = "phinn";
    nativeBuildInputs = [
      dotnet-sdk
      pkgs.bun
      pkgs.openfga-cli
      pkgs.npins
    ];
    DOTNET_ROOT = dotnet-sdk;
    DOTNET_CLI_TELEMETRY_OPTOUT = "true";
    NPINS_DIRECTORY = "nix";
  };
}
