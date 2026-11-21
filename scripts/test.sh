#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MOD_DIR="$ROOT_DIR/RandomSts1Mods"
STS2_DIR="${STS2_DIR:-$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2}"
STS2_BIN="$STS2_DIR/SlayTheSpire2"
DEPLOY_DIR="$STS2_DIR/mods/RandomSts1Mods"
HEADLESS_SETTINGS="$HOME/.local/share/SlayTheSpire2/default/1/settings.save"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

FORCE_PACK=0
for arg in "$@"; do
  case "$arg" in
    --pack) FORCE_PACK=1 ;;
    *) echo "Unknown arg: $arg" >&2; exit 2 ;;
  esac
done

# 1. Ensure isolated headless settings.save enables local mods (never touches Steam saves).
mkdir -p "$(dirname "$HEADLESS_SETTINGS")"
python3 - "$HEADLESS_SETTINGS" <<'PY'
import json, os, sys
path = sys.argv[1]
data = {}
if os.path.exists(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
data["seen_ea_disclaimer"] = True
data["skip_intro_logo"] = True
data["mod_settings"] = {
    "mods_enabled": True,
    "mod_list": [
        {"id": "BaseLibCompatibilityHelper", "is_enabled": True, "source": "mods_directory"},
        {"id": "BaseLib", "is_enabled": True, "source": "mods_directory"},
        {"id": "RandomSts1Mods", "is_enabled": True, "source": "mods_directory"},
    ],
}
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
PY

# 2. Build (fast dotnet build for code-only changes; dotnet publish when .pck needs refresh).
NEED_PACK=$FORCE_PACK
PCK_FILE="$DEPLOY_DIR/RandomSts1Mods.pck"
if [[ ! -f "$PCK_FILE" ]]; then
  NEED_PACK=1
elif [[ -n "$(find "$MOD_DIR/Packmaster" "$MOD_DIR/Downfall" "$MOD_DIR/RandomSts1Mods" "$MOD_DIR/images" "$MOD_DIR/project.godot" "$MOD_DIR/export_presets.cfg" -newer "$PCK_FILE" 2>/dev/null)" ]]; then
  NEED_PACK=1
fi

if [[ "$NEED_PACK" -eq 1 ]]; then
  echo "[test.sh] Running dotnet publish -c Debug (rebuilding .dll + .pck)..."
  (cd "$MOD_DIR" && dotnet publish -c Debug -v minimal)
else
  echo "[test.sh] Running dotnet build -c Debug (code-only fast path)..."
  (cd "$MOD_DIR" && dotnet build -c Debug -v minimal)
fi

# 3. Run headless game self-tests.
LOG_FILE="$(mktemp /tmp/packmaster_autotest.XXXXXX.log)"
trap 'rm -f "$LOG_FILE"' EXIT

echo "[test.sh] Launching headless SlayTheSpire2 autotest..."
set +e
DOWNFALL_AUTOTEST=0 PACKMASTER_AUTOTEST=1 timeout --kill-after=10 150 "$STS2_BIN" --headless --force-steam=off --audio-driver Dummy >"$LOG_FILE" 2>&1
RC=$?
set -e

grep -E '^\[AUTOTEST\]|^\[ERROR\]' "$LOG_FILE" || true

if [[ "$RC" -ne 0 ]] || ! grep -q '^\[AUTOTEST\] SUMMARY: ALL PASSED' "$LOG_FILE"; then
  echo "[test.sh] FAILED (exit=$RC). Last 60 lines of log:" >&2
  tail -n 60 "$LOG_FILE" >&2
  exit 1
fi

echo "[test.sh] SUCCESS!"
