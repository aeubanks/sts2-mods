#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MOD_DIR="$ROOT_DIR/Downfall"
STS2_DIR="${STS2_DIR:-$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2}"
STS2_BIN="$STS2_DIR/SlayTheSpire2"
DEPLOY_DIR="$STS2_DIR/mods/Downfall"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

FORCE_PACK=0
FILTER="${DOWNFALL_AUTOTEST_FILTER:-}"
JOBS="${DOWNFALL_AUTOTEST_JOBS:-}"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --pack) FORCE_PACK=1; shift ;;
    --filter)
      if [[ $# -lt 2 ]]; then echo "Missing argument for --filter" >&2; exit 2; fi
      FILTER="$2"; shift 2 ;;
    --jobs|-j)
      if [[ $# -lt 2 ]]; then echo "Missing argument for $1" >&2; exit 2; fi
      JOBS="$2"; shift 2 ;;
    *) echo "Unknown arg: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$JOBS" ]]; then
  if [[ -n "$FILTER" ]]; then
    JOBS=1
  else
    JOBS=4
  fi
fi

if [[ ! "$JOBS" =~ ^[1-9][0-9]*$ ]]; then
  echo "Invalid --jobs value: '$JOBS' (must be a positive integer)" >&2
  exit 2
fi

# 1. Build (fast dotnet build for code-only changes; dotnet publish when .pck needs refresh).
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

# 2. Helper to write an isolated settings.save into a per-shard XDG_DATA_HOME (never touches Steam saves).
write_settings() {
  local settings_path="$1"
  mkdir -p "$(dirname "$settings_path")"
  python3 - "$settings_path" "$STS2_DIR/mods" <<'PY'
import json, os, sys
path, mods_dir = sys.argv[1], sys.argv[2]
data = {
    "seen_ea_disclaimer": True,
    "skip_intro_logo": True,
    "mod_settings": {
        "mods_enabled": True,
        "mod_list": [
            {"id": "BaseLibCompatibilityHelper", "is_enabled": True, "source": "mods_directory"},
            {"id": "BaseLib", "is_enabled": True, "source": "mods_directory"},
        ] + ([{"id": "Packmaster", "is_enabled": True, "source": "mods_directory"}] if os.path.isdir(os.path.join(mods_dir, "Packmaster")) else [])
          + [{"id": "Downfall", "is_enabled": True, "source": "mods_directory"}],
    },
}
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
PY
}

# 3. Run headless game self-tests (parallel sharded with isolated XDG_DATA_HOME per worker).
WORK_DIR="$(mktemp -d /tmp/downfall_autotest.XXXXXX)"
PIDS=()
cleanup() {
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

echo "[test-downfall.sh] Launching headless SlayTheSpire2 autotest (jobs=$JOBS${FILTER:+, filter=$FILTER})..."
T_START=$(date +%s)

LOGS=()
for ((i = 0; i < JOBS; i++)); do
  SHARD_XDG="$WORK_DIR/xdg_$i"
  write_settings "$SHARD_XDG/SlayTheSpire2/default/1/settings.save"
  LOG_FILE="$WORK_DIR/shard_$i.log"
  LOGS+=("$LOG_FILE")
  SHARD_SPEC=""
  if [[ "$JOBS" -gt 1 ]]; then
    SHARD_SPEC="$i/$JOBS"
  fi
  XDG_DATA_HOME="$SHARD_XDG" \
  PACKMASTER_AUTOTEST=0 \
  DOWNFALL_AUTOTEST=1 \
  DOWNFALL_AUTOTEST_FILTER="$FILTER" \
  DOWNFALL_AUTOTEST_SHARD="$SHARD_SPEC" \
  timeout --kill-after=10 240 "$STS2_BIN" --headless --force-steam=off --audio-driver Dummy </dev/null >"$LOG_FILE" 2>&1 &
  PIDS+=($!)
done

ANY_FAIL=0
TOTAL_PASSED=0
for ((i = 0; i < JOBS; i++)); do
  set +e
  wait "${PIDS[$i]}"
  RC=$?
  set -e
  LOG_FILE="${LOGS[$i]}"
  if [[ "$JOBS" -gt 1 ]]; then
    echo "--- Shard $((i + 1))/$JOBS (exit=$RC) ---"
  fi
  grep -E '^\[AUTOTEST\]|^\[ERROR\]' "$LOG_FILE" || true
  if [[ "$RC" -ne 0 ]] || ! grep -q '^\[AUTOTEST\] SUMMARY: ALL PASSED' "$LOG_FILE"; then
    ANY_FAIL=1
    echo "[test-downfall.sh] Shard $((i + 1))/$JOBS FAILED (exit=$RC). Last 40 lines of log:" >&2
    tail -n 40 "$LOG_FILE" >&2
  else
    SHARD_PASS=$(grep -oE 'ALL PASSED \([0-9]+/' "$LOG_FILE" | grep -oE '[0-9]+' | head -n 1 || echo 0)
    TOTAL_PASSED=$((TOTAL_PASSED + SHARD_PASS))
  fi
done

T_END=$(date +%s)
ELAPSED=$((T_END - T_START))

if [[ "$ANY_FAIL" -ne 0 ]] || [[ "$TOTAL_PASSED" -le 0 ]]; then
  echo "[test-downfall.sh] FAILED (passed=$TOTAL_PASSED) in ${ELAPSED}s" >&2
  exit 1
fi

echo "[test-downfall.sh] SUCCESS! ($TOTAL_PASSED/$TOTAL_PASSED passed in ${ELAPSED}s wall-clock)"
