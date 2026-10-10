# CELESTAY Auto Idle Fishing Mod

[简体中文](README.zh-CN.md)

A small mod that lets CELESTAY's **Idle fishing mode** keep running without manually clicking each bite and catch result.

- Automatically reels in when a fish bites.
- Automatically closes the catch result after a configurable delay (1 second by default), allowing the next round to begin.
- Waits while menus or tutorials block fishing, and respects results you close manually.
- Leaves fishing timers, animations, catch selection, and rewards to the game.
- Does not automate Skill mode.

Background fishing continues only while the game itself keeps updating. The mod does not make the game run while the Mac is asleep.

**macOS only.** Windows has not been tested and is not supported by this release. Version 0.3.0 has been tried in-game on Apple Silicon; normal fishing, menu blocking, and manual result closing were checked. Special catches and tutorial sequences have not yet been fully verified in-game.

## Requirements

- The Steam version of CELESTAY for macOS. The current game app requires macOS 11 or later; this mod has only been tested on macOS 26.6.2, with Unity 6000.0.58f2. Older macOS versions and Intel Macs are untested.
- On Apple Silicon, **Rosetta 2**. Install it if macOS prompts you, or run `softwareupdate --install-rosetta` in Terminal and follow the prompts.
- **[BepInEx 5.4.23.5 for macOS](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip)**. Use this exact package for the instructions below. It includes Doorstop 4.5.0 and the Harmony dependencies; no separate downloads are needed.
- `CELESTAY.EnvironmentCheck.dll` from this project's binary release, and [prepare-macos-launcher.sh](scripts/prepare-macos-launcher.sh) from this repository. If no binary release is available yet, see **Build from source** below.

The launcher runs the game's Intel (`x86_64`) version through Rosetta on Apple Silicon to work around BepInEx 5's native ARM64 patching limitations. This applies to the whole game, not just the plugin. See the [upstream compatibility report](https://github.com/BepInEx/BepInEx/issues/1303).

## Install in four steps

1. **Quit CELESTAY and back up your saves.** In Steam, right-click CELESTAY → **Manage → Browse local files**. Keep this game folder open; it is the folder containing `CELESTAY.app`.

2. **Copy the files into the game folder.** Extract the BepInEx ZIP and copy its contents into that folder. Copy `prepare-macos-launcher.sh` there too. Create `BepInEx/plugins/CELESTAY.EnvironmentCheck/` and put `CELESTAY.EnvironmentCheck.dll` inside it. Keep only one copy of the plugin. The folder should look like this:

   ```text
   CELESTAY/
   ├── CELESTAY.app
   ├── libdoorstop.dylib
   ├── run_bepinex.sh
   ├── prepare-macos-launcher.sh
   └── BepInEx/
       ├── core/
       └── plugins/
           └── CELESTAY.EnvironmentCheck/
               └── CELESTAY.EnvironmentCheck.dll
   ```

   Copy the ZIP's contents directly into the game folder, without adding an extra enclosing folder. If you already use BepInEx for other mods, back up that installation before replacing anything.

3. **Prepare the launcher.** Open Terminal, type `cd ` (including the space), drag the game folder from Finder into Terminal, and press Return. Then run:

   ```sh
   sh ./prepare-macos-launcher.sh
   ```

   This creates `run_bepinex_x64.sh`, gives it permission to run, and prints a Steam launch-options line containing your actual game path. The original BepInEx script is kept. If macOS blocks downloaded files, see **Troubleshooting** below.

4. **Set Steam's launch options and play.** In Steam, right-click CELESTAY → **Properties → General → Launch Options**. Paste the line printed in step 3, including its quotes and `%command%`. It will look like this:

   ```text
   "/your/actual/game/folder/run_bepinex_x64.sh" %command%
   ```

   Launch the game through Steam and start **Idle** fishing. Auto fishing is enabled by default. To check that it loaded, open `BepInEx/LogOutput.log` in the game folder and look for `Loading [CELESTAY Environment Check 0.3.0]` and `AutoReady: enabled=True`. The internal plugin name is still “CELESTAY Environment Check”.

## Configuration

After the first launch, quit the game and edit `BepInEx/config/local.celestay.environmentcheck.cfg`:

```ini
[AutoIdleFishing]
Enabled = true
ResultCloseDelaySeconds = 1
```

- Set `Enabled = false` to turn off automation. Diagnostic logging stays enabled.
- Set `ResultCloseDelaySeconds` to a value from 0 to 60. The delay starts after the result is ready, following the animation and reward processing. A value of 0 closes it on the next eligible frame.

Restart the game after changing the configuration.

## Troubleshooting

- **The game runs, but fishing is not automated:** check that you are in Idle mode, the configuration is enabled, and Steam uses `run_bepinex_x64.sh`. The stock launcher can select ARM64 on Apple Silicon. Check the log mentioned in step 4.
- **macOS blocks the downloaded launcher or library:** if you trust the files downloaded from the links above, run these commands from the game folder to remove their quarantine attributes, then repeat step 3. This does not sign or notarize the files.

  ```sh
  xattr -d com.apple.quarantine ./prepare-macos-launcher.sh
  xattr -d com.apple.quarantine ./run_bepinex.sh
  xattr -d com.apple.quarantine ./libdoorstop.dylib
  ```

  “No such xattr” means that file does not have a quarantine attribute; continue with the next command. Do not disable Gatekeeper globally.
- **The log contains `AutoError` or an initialization error:** quit the game and keep a copy of `BepInEx/LogOutput.log` before starting it again. Game updates may break compatibility; remove the plugin if needed.

## Uninstall

Quit the game and remove `BepInEx/plugins/CELESTAY.EnvironmentCheck/`. If you want to stop using BepInEx entirely, clear the Steam launch options as well. Keep BepInEx installed if other mods use it.

## Build from source

Players using a compiled release do not need the .NET SDK. To build the plugin yourself, install the .NET SDK and BepInEx in the game folder, then run from this repository:

```sh
sh scripts/build.sh '-p:GameRoot=/absolute/path/to/CELESTAY'
```

The output is `outputs/CELESTAY.EnvironmentCheck.dll`. The build uses your installed game's assemblies and does not redistribute them. Rebuild against your local game if its assemblies change.

For local behavior checks:

```sh
dotnet run --project tests/AutoIdleFishingHarness.csproj --configuration Release
```

These checks use simulated game objects. They do not replace in-game testing. See [the trial notes](docs/auto-idle-fishing-trial.md) for the current verification scope.

## Development

This mod was made using **vibe coding**, with AI coding tools helping write the code and documentation. It has been tried in-game on my Mac, but testing is limited.

## Disclaimer

I made this mod because I wanted this feature for my own use. If it is useful to you, you are welcome to use it, but decide for yourself whether you are comfortable with the risks.

This is an unofficial mod, provided **as is**, without any warranty or promise of continued support. Back up your saves before installing it. To the extent permitted by applicable law, I am not responsible for damaged or lost game data, corrupted saves, crashes, or other problems caused by using this mod. Use it at your own risk.

## License

This project's code and documentation are licensed under the **[MIT License](LICENSE)**. BepInEx, Doorstop, and their dependencies retain their own licenses. CELESTAY and its assets belong to their respective owners and are not included in this repository.
