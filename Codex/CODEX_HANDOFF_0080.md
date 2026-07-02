# Codex Handoff — Patch 0080

## Use This Package As Input
Use this package as the current cumulative project data snapshot. It includes a full recreated `Data/` folder and all Patch 0080 reports.

## Do Not Use
Do not depend on prior ZIPs from inside this package; old ZIPs are intentionally excluded.

## Important Files
- `Data/` — recreated runtime data snapshot
- `Reports/PATCH_0080_ROOM_EXIT_SPECIAL_EXIT_REPORT.md` — human summary
- `Reports/room_exit_endpoint_recovery_0080.csv` — complete endpoint recovery audit
- `Reports/runtime_parity_issues_remaining_0080.csv` — remaining parity blockers
- `Reports/special_exit_type_semantics_0080.csv` — special exit evidence classification
- `ValidationOverlays/patch0080_room_exit_special_exit_overlay.json` — machine-readable patch summary

## Hard Rules
- Do not create synthetic rooms for unresolved endpoints.
- Do not promote special-exit behavior from `exit_type` values alone.
- Preserve unknown fields and blocked relationships until direct evidence exists.
