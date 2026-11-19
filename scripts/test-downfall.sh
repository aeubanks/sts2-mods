#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MOD_DIR="$ROOT_DIR/Downfall"
STS2_DIR="${STS2_DIR:-$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2}"
STS2_BIN="$STS2_DIR/SlayTheSpire2"
DEPLOY_DIR="$STS2_DIR/mods/Downfall"
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

# 1. Ensure isolated headless settings.save enables Downfall (+ Packmaster if deployed, never touches Steam saves).
# NOTE: Do not run test.sh and test-downfall.sh concurrently because both write this isolated settings.save.
mkdir -p "$(dirname "$HEADLESS_SETTINGS")"
python3 - "$HEADLESS_SETTINGS" "$STS2_DIR/mods" <<'PY'
import json, os, sys
path, mods_dir = sys.argv[1], sys.argv[2]
data = {}
if os.path.exists(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
data["seen_ea_disclaimer"] = True
data["skip_intro_logo"] = True
mod_list = [
    {"id": "BaseLibCompatibilityHelper", "is_enabled": True, "source": "mods_directory"},
    {"id": "BaseLib", "is_enabled": True, "source": "mods_directory"},
]
if os.path.isdir(os.path.join(mods_dir, "Packmaster")):
    mod_list.append({"id": "Packmaster", "is_enabled": True, "source": "mods_directory"})
mod_list.append({"id": "Downfall", "is_enabled": True, "source": "mods_directory"})
data["mod_settings"] = {
    "mods_enabled": True,
    "mod_list": mod_list,
}
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
PY

# 2. Build (fast dotnet build for code-only changes; dotnet publish when .pck needs refresh).
NEED_PACK=$FORCE_PACK
PCK_FILE="$DEPLOY_DIR/Downfall.pck"
if [[ ! -f "$PCK_FILE" ]]; then
  NEED_PACK=1
elif [[ -n "$(find "$MOD_DIR/Downfall" "$MOD_DIR/project.godot" "$MOD_DIR/export_presets.cfg" -newer "$PCK_FILE" 2>/dev/null)" ]]; then
  NEED_PACK=1
fi

if [[ "$NEED_PACK" -eq 1 ]]; then
  echo "[test-downfall.sh] Running dotnet publish -c Debug (rebuilding .dll + .pck)..."
  (cd "$MOD_DIR" && dotnet publish -c Debug -v minimal)
else
  echo "[test-downfall.sh] Running dotnet build -c Debug (code-only fast path)..."
  (cd "$MOD_DIR" && dotnet build -c Debug -v minimal)
fi

# 3. Run headless game self-tests (explicitly disabling Packmaster autotest to prevent race on Quit()).
LOG_FILE="$(mktemp /tmp/downfall_autotest.XXXXXX.log)"
trap 'rm -f "$LOG_FILE"' EXIT

echo "[test-downfall.sh] Launching headless SlayTheSpire2 autotest..."
set +e
PACKMASTER_AUTOTEST=0 DOWNFALL_AUTOTEST=1 timeout --kill-after=10 240 "$STS2_BIN" --headless --force-steam=off --audio-driver Dummy >"$LOG_FILE" 2>&1
RC=$?
set -e

grep -E '^\[AUTOTEST\]|^\[ERROR\]' "$LOG_FILE" || true

if [[ "$RC" -ne 0 ]] || ! grep -q '^\[AUTOTEST\] SUMMARY: ALL PASSED' "$LOG_FILE"; then
  echo "[test-downfall.sh] FAILED (exit=$RC). Last 60 lines of log:" >&2
  tail -n 60 "$LOG_FILE" >&2
  exit 1
fi

echo "[test-downfall.sh] SUCCESS!"
