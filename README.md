# Blish HUD for macOS (Wine / GameHub) — unofficial fork

> **This is an unofficial fork** of [Blish HUD](https://github.com/blish-hud/Blish-HUD), patched so it runs on **macOS** next to Guild Wars 2 running through **Wine** (tested with GameHub on Apple Silicon).
> It is **not affiliated** with the Blish HUD team. Please do not report issues of this fork to them.

Tested on: Apple M4 Max, macOS 26, GameHub with `wine-proton_11.0` + GPTK 3, Blish HUD 1.3.0 base.

## What is fixed

Stock Blish HUD runs under Wine but is unusable on macOS for three reasons. Both fixes only activate when Wine is detected (`ntdll!wine_get_version`); on Windows the behavior is unchanged.

| Problem on macOS / Wine | Cause | Fix |
|---|---|---|
| Crash a few seconds after GW2 is detected (`NotImplementedException` in `NAudio.CoreAudioApi.AudioSessionManager.UnregisterNotifications`) | Wine does not implement `IAudioSessionManager2::UnregisterSessionNotification`; NAudio's finalizer throws on the finalizer thread and kills the process | Per-process audio session tracking is skipped under Wine; the overlay volume uses the volume setting instead of following the game |
| Overlay is an opaque black window | Wine has no DWM composition (`DwmExtendFrameIntoClientArea` is a stub), and on macOS the D3D/Metal swap chain surface covers the window | Under Wine, frames are rendered into a hidden device window, read back from the back buffer and pushed to the overlay window with `UpdateLayeredWindow` (per-pixel alpha), which Wine's macOS driver supports |
| Clicking the overlay disables keyboard/mouse input (text boxes cannot be typed in, icons need several clicks) | On macOS, clicking the overlay leaves the desktop window (`#32769`) as Wine's foreground window, so GW2 is considered unfocused and the input hooks are disabled | Under Wine the overlay gets `WS_EX_NOACTIVATE`; if the desktop is foreground while the cursor is over GW2, GW2 is still considered focused |

Code: [`AudioIntegration.cs`](Blish%20HUD/GameServices/GameIntegration/AudioIntegration.cs), [`WineLayeredPresenter.cs`](Blish%20HUD/_Utils/WineLayeredPresenter.cs), [`BlishHud.cs`](Blish%20HUD/BlishHud.cs), [`WindowUtil.cs`](Blish%20HUD/_Utils/WindowUtil.cs).
Standalone test of per-pixel alpha under Wine: [`wine-overlay-test/layered_test.c`](wine-overlay-test/layered_test.c).

## Install with GameHub

1. Install and run Guild Wars 2 in GameHub (local game import of `Gw2-64.exe`).
2. Download the official [Blish HUD 1.3.0](https://github.com/blish-hud/Blish-HUD/releases/tag/v1.3.0) zip and extract it into the GW2 container, e.g.
   `~/Library/Application Support/com.gamemac.www/wine-engine/containers/virtual_containers/<id>/drive_c/Blish HUD/`
3. Build this fork (see below) and replace `Blish HUD.exe` in that folder with the patched one (keep a copy of the original).
4. Copy [`macos/launch-blish-hud.command`](macos/launch-blish-hud.command) somewhere convenient and make it executable.
5. In GW2: **Options → Graphics → Display mode: Windowed** (maximize the window). In fullscreen, macOS puts the game in its own Space and the overlay cannot be drawn above it.
6. Start GW2 from GameHub, load a character, then run `launch-blish-hud.command` (pass the container id if it is not `3`).

The launcher reuses the Wine environment of the last GW2 launch (read from GameHub's `wine-logs`), so Blish HUD runs in the same wineserver as the game, which is required for MumbleLink.

## Build

The build uses the upstream Windows CI. On your fork: **Actions → PR → Run workflow** on the `wine-audio-fix` branch, then download the `Blish HUD <version>` artifact and take `Blish HUD.exe` from it.
Push the upstream tags to your fork first (`git push <fork> v1.3.0`), otherwise GitVersion produces a `0.x` version that modules refuse.

## Options (environment variables)

| Variable | Effect |
|---|---|
| `BLISHHUD_WINE_LAYERED=0` | Disable the `UpdateLayeredWindow` presenter (back to the black window) |
| `BLISHHUD_WINE_COLORKEY=1` | Experimental black color-key transparency (does not work with D3D rendering on macOS) |

## Known limitations

- **Windowed mode only** (see above).
- **Do not accept Blish HUD self-updates**: they replace the patched executable.
- The overlay volume does not follow the game volume; set it in Blish HUD's settings.
- Reading back every frame costs CPU (~0.7 core on an M4 Max at 1728×994). Lower Blish HUD's frame limit if needed.
- While GW2's chat input has focus, Blish HUD text boxes do not receive keys (same as on Windows): leave the chat first.
- After clicking Blish HUD, switching to another macOS app with the cursor still over the game may leave the overlay on top.
- `Cmd+Q` hides Blish HUD to the background instead of quitting; the launcher stops a lingering instance before starting a new one.

---

# Blish HUD
[![Discord](https://img.shields.io/discord/531175899588984842.svg?logo=discord&logoColor=%237289DA)](https://discord.gg/FYKN3qh)

**Visit our site:** https://blishhud.com

## Check our Setup Guide

[![Blish HUD Setup Video](https://img.youtube.com/vi/iLYYumF2SCY/0.jpg)](https://www.youtube.com/watch?v=iLYYumF2SCY)

## Download Blish HUD

You can download Blish HUD:
- Using the [direct download (v1.2.0)](https://github.com/blish-hud/Blish-HUD/releases/download/v1.2.0/Blish.HUD.1.2.0.zip) link.
- From our [Releases](https://github.com/blish-hud/Blish-HUD/releases) page here on GitHub.

### Need Help?

Visit our [#💢help](https://discord.gg/qJdUhdG) channel in Discord.

## Links of Interest

### Blish HUD Resources

- [BlishHUD.com](https://blishhud.com/) - Our website.
- [Blish HUD FAQ](https://blishhud.com/docs/user/faq) - Frequently asked questions.
- [Troubleshooting Guide](https://blishhud.com/docs/user/troubleshooting/) - Our troubleshooting guide.
- [Arcdps Blish HUD Integration](https://github.com/blish-hud/arcdps-bhud) - a plugin that uses the Arcdps Combat API and exposes some of the data to Blish HUD for compatible modules.

## For Developers

Pull requests are welcome. You are encouraged to join the discussion in the [Blish HUD #🔨core_discussion Discord channel](https://discord.gg/nGbd3kU).

### Build Requirements

#### Prerequisites

- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
- Individual Components that need to be installed in Visual Studio via the Visual Studio Installer:
  - .NET Core 3.1 Runtime 
  - .NET Framework 4.7.2 SDK
  - .NET Framework 4.7.2 targeting pack
  - Visual C++ Redistributable 2012

#### Instructions

1.  Clone the repo: `git clone -v --recurse-submodules --progress  https://github.com/blish-hud/Blish-HUD.git`
2.  Launch the project solution (.sln file) in Visual Studio 2022.
3.  In the Solution Explorer right click on the solution icon. In the context menu click "Restore NuGet Packages".
4.  Right click the solution icon again and click "Build Solution". 

### Module Development

- [Visual Studio 2019 Module Template](https://github.com/blish-hud/Module-Template)
- [Module Documentation](https://blishhud.com/docs/dev/)
- [Example Module](https://github.com/blish-hud/Example-Blish-HUD-Module/blob/master/README.md)

### Thanks

Thank you [JetBrains](https://www.jetbrains.com/?from=Blish%20HUD) for providing free open source licenses to our primary contributers.

### License

Licensed under the [MIT License](https://choosealicense.com/licenses/mit/)
