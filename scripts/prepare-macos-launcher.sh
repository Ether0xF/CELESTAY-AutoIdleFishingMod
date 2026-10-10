#!/bin/sh
# Prepare the BepInEx 5.4.23.5 launcher for CELESTAY on macOS.
# Run this script after copying it into the folder containing CELESTAY.app.
set -eu

GAME_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
cd "$GAME_DIR"

if [ ! -d CELESTAY.app ] || [ ! -f run_bepinex.sh ] ||
   [ ! -f libdoorstop.dylib ] || [ ! -f BepInEx/core/BepInEx.Preloader.dll ]; then
    printf '%s\n' 'Copy this script and the BepInEx macOS package into the folder containing CELESTAY.app first.' >&2
    exit 1
fi

# Reject other launcher versions rather than silently generating a broken one.
if ! grep -q '^executable_name=""$' run_bepinex.sh ||
   ! grep -q 'export ARCHPREFERENCE="arm64,x86_64"' run_bepinex.sh ||
   ! grep -q 'exec arch -e DYLD_INSERT_LIBRARIES=' run_bepinex.sh; then
    printf '%s\n' 'Expected the unmodified launcher from BepInEx_macos_universal_5.4.23.5.zip.' >&2
    exit 1
fi

LAUNCHER_TEMP=$(mktemp "$GAME_DIR/.run_bepinex_x64.XXXXXX")
trap 'rm -f "$LAUNCHER_TEMP"' EXIT HUP INT TERM
sed \
    -e 's/^executable_name=""$/executable_name="CELESTAY.app"/' \
    -e 's/export ARCHPREFERENCE="arm64,x86_64"/export ARCHPREFERENCE="x86_64"/' \
    -e 's/exec arch -e DYLD_INSERT_LIBRARIES=/exec \/usr\/bin\/arch -x86_64 -e DYLD_INSERT_LIBRARIES=/' \
    run_bepinex.sh > "$LAUNCHER_TEMP"
/bin/sh -n "$LAUNCHER_TEMP"
chmod u+x "$LAUNCHER_TEMP"
mv "$LAUNCHER_TEMP" run_bepinex_x64.sh

printf '%s\n' 'Launcher ready. Paste this line into CELESTAY’s Steam Launch Options:'
printf '"%s/run_bepinex_x64.sh" %%command%%\n' "$GAME_DIR"
