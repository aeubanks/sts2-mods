#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MOD_DIR="$ROOT_DIR/RandomSts1Mods"
STS2_DIR="${STS2_DIR:-$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2}"
STS2_BIN="$STS2_DIR/SlayTheSpire2"
DEPLOY_DIR="$STS2_DIR/mods/RandomSts1Mods"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

FORCE_PACK=0
SCENARIOS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --pack) FORCE_PACK=1; shift ;;
    --scenario)
      if [[ $# -lt 2 ]]; then echo "Missing argument for --scenario" >&2; exit 2; fi
      SCENARIOS+=("$2"); shift 2 ;;
    *) echo "Unknown arg: $1" >&2; exit 2 ;;
  esac
done

if [[ ${#SCENARIOS[@]} -eq 0 ]]; then
  SCENARIOS=("dual_packmaster" "mutator_and_architect")
fi

# 1. Build (fast dotnet build for code-only changes; dotnet publish when .pck needs refresh).
NEED_PACK=$FORCE_PACK
PCK_FILE="$DEPLOY_DIR/RandomSts1Mods.pck"
if [[ ! -f "$PCK_FILE" ]]; then
  NEED_PACK=1
elif [[ -n "$(find "$MOD_DIR/Downfall" "$MOD_DIR/Packmaster" "$MOD_DIR/RandomSts1Mods" "$MOD_DIR/images" "$MOD_DIR/project.godot" "$MOD_DIR/export_presets.cfg" -newer "$PCK_FILE" 2>/dev/null)" ]]; then
  NEED_PACK=1
fi

if [[ "$NEED_PACK" -eq 1 ]]; then
  echo "[test-multiplayer.sh] Running dotnet publish -c Debug (rebuilding .dll + .pck)..."
  (cd "$MOD_DIR" && dotnet publish -c Debug -v minimal)
else
  echo "[test-multiplayer.sh] Running dotnet build -c Debug (code-only fast path)..."
  (cd "$MOD_DIR" && dotnet build -c Debug -v minimal)
fi

WORK_DIR="$(mktemp -d /tmp/sts2_mp_autotest.XXXXXX)"
PIDS=()
cleanup() {
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

write_settings() {
  local settings_path="$1"
  mkdir -p "$(dirname "$settings_path")"
  python3 - "$settings_path" <<'PY'
import json, sys
path = sys.argv[1]
data = {
    "seen_ea_disclaimer": True,
    "skip_intro_logo": True,
    "mod_settings": {
        "mods_enabled": True,
        "mod_list": [
            {"id": "BaseLibCompatibilityHelper", "is_enabled": True, "source": "mods_directory"},
            {"id": "BaseLib", "is_enabled": True, "source": "mods_directory"},
            {"id": "RandomSts1Mods", "is_enabled": True, "source": "mods_directory"},
        ],
    },
}
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
PY
}

pick_free_port() {
  python3 -c 'import socket; s=socket.socket(socket.AF_INET, socket.SOCK_DGRAM); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()'
}

run_scenario() {
  local scenario="$1"
  local port
  port="$(pick_free_port)"
  local host_xdg="$WORK_DIR/${scenario}_host_xdg"
  local client_xdg="$WORK_DIR/${scenario}_client_xdg"
  local host_log="$WORK_DIR/${scenario}_host.log"
  local client_log="$WORK_DIR/${scenario}_client.log"

  write_settings "$host_xdg/SlayTheSpire2/default/1/settings.save"
  write_settings "$client_xdg/SlayTheSpire2/default/1/settings.save"

  echo "[test-multiplayer.sh] === Running 2-process ENet scenario: $scenario (port=$port) ==="

  XDG_DATA_HOME="$host_xdg" \
  MP_AUTOTEST=host \
  MP_PORT="$port" \
  MP_SCENARIO="$scenario" \
  timeout --kill-after=10 120 "$STS2_BIN" --headless --force-steam=off --audio-driver Dummy </dev/null >"$host_log" 2>&1 &
  local host_pid=$!
  PIDS+=("$host_pid")

  XDG_DATA_HOME="$client_xdg" \
  MP_AUTOTEST=client \
  MP_PORT="$port" \
  MP_SCENARIO="$scenario" \
  timeout --kill-after=10 120 "$STS2_BIN" --headless --force-steam=off --audio-driver Dummy </dev/null >"$client_log" 2>&1 &
  local client_pid=$!
  PIDS+=("$client_pid")

  set +e
  wait "$host_pid"
  local host_rc=$?
  wait "$client_pid"
  local client_rc=$?
  set -e

  echo "--- Host ($scenario, exit=$host_rc) ---"
  grep -E '^\[MP-TEST:|^\[ERROR\]|Node needs a parent|Child is not a child|StateDiverg|NullReferenceException' "$host_log" || true
  echo "--- Client ($scenario, exit=$client_rc) ---"
  grep -E '^\[MP-TEST:|^\[ERROR\]|Node needs a parent|Child is not a child|StateDiverg|NullReferenceException' "$client_log" || true

  local failed=0
  if [[ "$host_rc" -ne 0 ]] || ! grep -q "^\[MP-TEST:host\] PASS $scenario" "$host_log"; then
    echo "[test-multiplayer.sh] Host FAILED for $scenario (exit=$host_rc). Tail of host log:" >&2
    tail -n 45 "$host_log" >&2
    failed=1
  fi
  if [[ "$client_rc" -ne 0 ]] || ! grep -q "^\[MP-TEST:client\] PASS $scenario" "$client_log"; then
    echo "[test-multiplayer.sh] Client FAILED for $scenario (exit=$client_rc). Tail of client log:" >&2
    tail -n 45 "$client_log" >&2
    failed=1
  fi
  if grep -qE 'Node needs a parent to be reparented|Child is not a child of this node|StateDiverg|NullReferenceException' "$host_log" "$client_log"; then
    echo "[test-multiplayer.sh] Detected forbidden multiplayer error in logs for $scenario!" >&2
    failed=1
  fi

  return "$failed"
}

T_START=$(date +%s)
ANY_FAIL=0
for sc in "${SCENARIOS[@]}"; do
  if ! run_scenario "$sc"; then
    ANY_FAIL=1
  fi
done
T_END=$(date +%s)

if [[ "$ANY_FAIL" -ne 0 ]]; then
  echo "[test-multiplayer.sh] FAILED in $((T_END - T_START))s" >&2
  exit 1
fi

echo "[test-multiplayer.sh] SUCCESS! All ${#SCENARIOS[@]} 2-process ENet multiplayer scenarios passed in $((T_END - T_START))s!"
