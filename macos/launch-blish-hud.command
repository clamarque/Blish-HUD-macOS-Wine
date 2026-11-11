#!/bin/bash
# Launches Blish HUD inside the GameHub Wine container that runs Guild Wars 2.
# Start GW2 from GameHub first (windowed mode, character loaded), then run this script.
# It reuses the Wine environment of the last GW2 launch (read from the GameHub log),
# so Blish HUD shares the same wineserver as the game (required for MumbleLink).
#
# Usage: launch-blish-hud.command [container-id]   (default: 3)
set -euo pipefail

CONTAINER="${1:-3}"
ENGINE="$HOME/Library/Application Support/com.gamemac.www/wine-engine"
LOG=$(ls -t "$ENGINE"/wine-logs/wine-launch-*-"$CONTAINER".log 2>/dev/null | head -1)
[ -n "$LOG" ] || { echo "No GW2 launch log found for container $CONTAINER. Start GW2 from GameHub first."; exit 1; }

if ! pgrep -f "Gw2-64.exe" >/dev/null; then
  echo "GW2 is not running. Start it from GameHub, then run this script again."
  exit 1
fi

# Stop a Blish HUD instance left running in the background (Cmd+Q does not always quit it)
if pgrep -f "Blish HUD.exe" >/dev/null; then
  pkill -f "Blish HUD.exe" || true
  for i in $(seq 1 10); do pgrep -f "Blish HUD.exe" >/dev/null || break; sleep 1; done
  pkill -9 -f "Blish HUD.exe" 2>/dev/null || true
fi

# Environment from the GameHub launch (GameHub's monitor and activity tag are left out)
KEEP='^(DYLD_FALLBACK_LIBRARY_PATH|LANG|LC_ALL|PATH|PROCESS_TAG|SANDBOXFS_LAYER_MANIFEST|SANDBOXFS_LIB_PATH|WINEARCH|WINEDLLPATH|WINEENGINE_GPTK_DXGI_SPOOF_ENABLED|WINEMSYNC|WINEPREFIX|WINEPREFIX_BASE|WINE_GPTK_LIBD3DSHARED_PATH|WINE_INSTALLATION_PATH|WINE_PATH)='
while IFS= read -r kv; do
  export "$kv"
done < <(grep -a -o -E '\] env\.[A-Z_]+=.*' "$LOG" | sed -E 's/^\] env\.//' | grep -E "$KEEP")

# Keep only libsandboxfs (layered filesystem of the GameHub container)
export DYLD_INSERT_LIBRARIES="$SANDBOXFS_LIB_PATH"
export WINEDEBUG=-all

WINE=$(grep -a -o -E 'command_executable=.*' "$LOG" | head -1 | sed 's/^command_executable=//')
echo "Starting Blish HUD with $WINE"
exec "$WINE" "C:\\Blish HUD\\Blish HUD.exe" > "$HOME/GameHub/blish-hud.log" 2>&1
