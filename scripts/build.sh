#!/bin/sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_BIN=$(command -v dotnet)
else
    DOTNET_BIN=/usr/local/share/dotnet/dotnet
fi

export DOTNET_CLI_HOME="$ROOT/work/dotnet-home"
export NUGET_PACKAGES="$ROOT/work/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export DOTNET_NOLOGO=1

"$DOTNET_BIN" build "$ROOT/src/CELESTAY.EnvironmentCheck/CELESTAY.EnvironmentCheck.csproj" \
    --configuration Release "$@"

mkdir -p "$ROOT/outputs"
cp "$ROOT/src/CELESTAY.EnvironmentCheck/bin/Release/netstandard2.1/CELESTAY.EnvironmentCheck.dll" \
    "$ROOT/outputs/CELESTAY.EnvironmentCheck.dll"
printf 'Plugin built: %s\n' "$ROOT/outputs/CELESTAY.EnvironmentCheck.dll"
