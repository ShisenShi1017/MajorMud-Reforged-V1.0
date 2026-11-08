#!/usr/bin/env bash
# RealmGate Linux diagnostics. Captures stdout/stderr and read-only spawn metadata.
# Does not change RealmGate saves, configuration, or generated world data.
set -uo pipefail

script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)"
if [ "$#" -ge 1 ]; then app="$1"; else app="$script_dir/RealmGate.Desktop"; fi
case "$app" in
  /*) ;;
  *) app="$(pwd)/$app" ;;
esac
if [ ! -f "$app" ]; then
  printf 'RealmGate executable not found: %s\n' "$app" >&2
  printf 'Usage: bash diag.sh [./RealmGate.Desktop] [output-directory]\n' >&2
  exit 2
fi

app_dir="$(CDPATH= cd -- "$(dirname -- "$app")" && pwd -P)"
app_name="$(basename -- "$app")"
if [ "$#" -ge 2 ]; then out="$2"; else out="$app_dir/RealmGate-Diagnostics-$(date +%Y%m%d-%H%M%S)"; fi
if ! mkdir -p -- "$out"; then
  printf 'Cannot create output directory: %s\n' "$out" >&2
  exit 2
fi
out="$(cd -- "$out" && pwd -P)"

{
  printf 'Capture: %s\nExecutable: %s\nWorkdir: %s\n' "$(date -Is)" "$app" "$app_dir"
  printf '\n-- System --\n'
  uname -a || true
  if [ -r /etc/os-release ]; then sed -n '1,12p' /etc/os-release; fi
  printf '\n-- Memory --\n'
  command -v free >/dev/null && free -h || true
  printf '\n-- Application metadata --\n'
  command -v file >/dev/null && file "$app" || true
  command -v sha256sum >/dev/null && sha256sum "$app" || true
  printf '\n-- dotnet --\n'
  if command -v dotnet >/dev/null; then dotnet --info | head -45; else echo 'System dotnet not present (self-contained package is supported).'; fi
} > "$out/environment.txt" 2>&1

spawn_report() {
  local stage="$1"
  if ! command -v python3 >/dev/null; then
    printf 'python3 unavailable: cannot inspect generated spawn JSON\n' > "$out/spawns-$stage.txt"
    return
  fi
  RG_ROOT="$app_dir" python3 - > "$out/spawns-$stage.txt" 2>&1 <<'PY'
import json, os, pathlib
root = pathlib.Path(os.environ['RG_ROOT'])
classic = root / 'Data' / 'Classic'
print('Generated data root:', classic)
for name in ('rooms.json', 'npcs.json', 'spawns.json', 'world_manifest.json', 'compile_manifest.json'):
    p = classic / name
    print(name, f'{p.stat().st_size} bytes' if p.is_file() else 'MISSING')
p = classic / 'spawns.json'
if not p.is_file():
    print('No generated spawns.json here; this may be before first Host start or a different data root.')
    raise SystemExit(0)
try:
    data = json.loads(p.read_text(encoding='utf-8-sig'))
    if isinstance(data, list):
        rows = data
    elif isinstance(data, dict):
        rows = next((data[k] for k in ('records', 'spawns', 'entries', 'items') if isinstance(data.get(k), list)), [])
        print('JSON top-level keys:', ', '.join(list(data)[:25]))
    else:
        rows = []
    print('Spawn records parsed:', len(rows))
    if rows and isinstance(rows[0], dict):
        print('Sample spawn keys:', ', '.join(list(rows[0])[:35]))
    positive_caps = 0
    zero_caps = 0
    arena = []
    for r in rows:
        if not isinstance(r, dict):
            continue
        model = r.get('spawn_model')
        model = model if isinstance(model, dict) else r
        cap = model.get('native_room_generation_cap', r.get('native_room_generation_cap'))
        if cap is not None:
            try:
                if int(cap) > 0: positive_caps += 1
                else: zero_caps += 1
            except (ValueError, TypeError):
                pass
        map_id = r.get('source_map_id', r.get('map_id', model.get('map_id')))
        room = r.get('source_room_number', r.get('room_number', model.get('room_number')))
        if str(map_id) == '1' and str(room) in ('2150', '2155'):
            arena.append(r)
    print('Native room generation caps: positive=', positive_caps, 'zero=', zero_caps)
    print('Newhaven Arena spawn records:', len(arena))
    for record in arena[:15]:
        print(' ', json.dumps(record, ensure_ascii=True, separators=(',', ':'))[:500])
except Exception as exc:
    print('Failed to parse spawns.json:', type(exc).__name__, str(exc))
PY
}

spawn_report before
printf '\nRealmGate diagnostics output: %s\n' "$out"
printf 'Reproduce the arena problem and/or run beyond three minutes, then close the app.\n'
cd -- "$app_dir" || exit 2
start="$(date +%s)"
set +e
"./$app_name" 2>&1 | tee "$out/application.log"
code=$?
set -e
end="$(date +%s)"
printf 'started_epoch=%s\nended_epoch=%s\nrun_seconds=%s\nexit_code=%s\n' \
  "$start" "$end" "$((end - start))" "$code" > "$out/exit-status.txt"
spawn_report after
if command -v coredumpctl >/dev/null; then
  coredumpctl --no-pager --since "@$start" list > "$out/coredump-list.txt" 2>&1 || true
fi
printf '\nDiagnostics saved in: %s\nExit code: %s\n' "$out" "$code"
printf 'Review application.log before sharing: it can include character names or commands.\n'
exit 0
