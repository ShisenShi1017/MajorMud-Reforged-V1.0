# CodeUpdate 0079 — Host Lifecycle Evidence Lockdown

## Result

Status: **PASS_WITH_LIMITATIONS**.

0079 converts the 0078 host-timing hypothesis into an evidence-backed host lifecycle model using the MBBSEmu GitHub source as an external evidence repository.

## Main finding

MajorMUD timing should be modeled as **host/module-driven**, not client-side and not modem-ping-driven. Modem or Telnet latency affects what the player sees, but current evidence does not show it as the authoritative gameplay clock.

## Reconstructed lifecycle

1. Wait for next host tick if idle.
2. Process nightly cleanup.
3. Process incoming sessions and disconnects.
4. Iterate channels serially.
5. Process one client byte / GSBL input / character input.
6. Run global handlers where applicable.
7. For in-module users: process STSROU/status events.
8. For in-module users: process STTROU command events.
9. For in-module users: process BEGIN_POLLING routine if registered.
10. After all channels: process RTKICK.
11. Process RTIHDLR.
12. Process SYSCYC.
13. Process INITASK/tasks.
14. Drain/dequeue pending statuses.

## Timing conclusions

- `TICKER` increments once per second in GALGSBL evidence.
- `RTKICK` is seconds-delay, one-shot behavior.
- `RTIHDLR` is approximately 18Hz / 55ms.
- `BEGIN_POLLING` is per-channel and called as often as possible while registered.
- `Timer.Hertz` gates idle host loop wakeups, but pending input/output/status bypasses the wait.
- Output is queued and drained by the session sender path; heartbeat/Telnet negotiation is separate from gameplay timing.

## What changed

- Added `Librarium/HostLifecycleVerification/host_lifecycle_model_0079.json`.
- Added `Librarium/HostLifecycleVerification/host_timing_state_machine_0079.json`.
- Added `Evidence/HostLifecycle0079/host_lifecycle_evidence_sources_0079.json`.
- Added `Librarium/GapRegistry/host_lifecycle_gaps_0079.json`.
- Added `Validation/HostLifecycle0079/host_lifecycle_probe_plan_0079.json`.
- Added query helper `Tools/Librarium/query_host_lifecycle_0079.py`.

## Promotions

- Formulas promoted: **0**.
- Runtime formulas replaced: **0**.
- Host timing model promoted only to **Derived/Observed**, not Verified Classic truth.

## Why not Verified?

MBBSEmu is strong implementation evidence, but the Librarium rules require corroboration before treating behavior as Verified Classic MajorMUD/MajorBBS truth. The next step is trace-based runtime verification of callback registration and timing cadence.
