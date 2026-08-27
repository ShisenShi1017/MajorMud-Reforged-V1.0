# Code Update 0285 — Borin Academy Historical Replay Repair

## Purpose

Repair the single failing Borin self-play validation case reported after the successful Release build. The Academy historical replay test creates verified evidence under `Release/Logs/verified.json`, but the replay allowlist did not scan `Release/Logs`, so the verified episode could never be imported by reference.

## Change

`BorinAcademy.ReplayHistoryAsync` now scans these additional evidence locations:

- `Release/Logs/**/*.json`
- `Release/Logs/**/*.log`
- `Release/Logs/**/*.txt`

The existing verification gate remains unchanged: a file must contain an explicit `PASS` or `verified` marker and must not contain `0 passed`.

Historical replay remains reference-based. The Academy stores the evidence path and extracted canonical entity IDs; it does not copy raw log contents into durable knowledge.

## Validation target

Re-run:

```powershell
.\Tools\Borin\run_self_play_validation.ps1
```

The previously failing case should now pass:

`Academy generates, scores, gaps, replays, and invalidates safely`

This repair does not alter Borin's autonomous gameplay, cleanup/reconnect, Librarium decision support, questing, inventory/shop management, memory synchronization, or public-release isolation behavior.
