# CodeUpdate 0078 — Host Execution Model, World Loop, and Behavior Timelines

Status: **PASS_WITH_LIMITATIONS**  
Baseline: CodeUpdate 0077 cumulative repository  
Canonical rule: **/Librarium remains the source of truth. External repositories are evidence only.**

## Scope

This update completes the requested Phases 1–3:

1. Host Execution Model
2. World Loop Reconstruction
3. Behavior Timeline Mapping

No gameplay formula was promoted to Verified, and no runtime formula was replaced.

## Main finding

MajorMUD timing should be modeled as **host-driven**, not client-driven. The current evidence supports a MajorBBS/Worldgroup-style model where the host invokes module routines, handles per-session input/status/polling, processes timed callbacks, and then player-visible output is delivered through the session connection.

The user's modem/ping hypothesis is important, but the current evidence distinguishes two layers:

- **Authoritative state timing:** host/module callbacks and timers.
- **Player-visible delivery timing:** modem/telnet/session output delivery.

So the stronger working model is: modem latency affects when the user sees output, but the gameplay tick itself is controlled by host/module scheduling.

## Evidence from MBBSEmu

MBBSEmu exposes several timing primitives relevant to MajorBBS/Worldgroup modules:

- `TICKER` — exported global updated once per second.
- `BEGIN_POLLING` — per-channel routine called as fast/often as possible until stopped.
- `STSROU` — status/deferred processing routine.
- `RTKICK` — one-shot delayed callbacks in seconds.
- `RTIHDLR` — approximately 18Hz / 55ms repeated callbacks.
- `SYSCYC` — module cycle callback if the module sets a pointer.
- `INITASK` — very frequent task callback.

These establish the host timing substrate. They do **not** yet prove exactly which one Classic MajorMUD used for every subsystem.

## Phase 1 — Host Execution Model

Created:

- `Librarium/HostExecutionModel/host_execution_model_0078.json`
- `Evidence/HostExecutionModel0078/host_execution_evidence_sources_0078.json`

Key model:

1. Host owns authoritative module execution.
2. Session states determine which module routine is called.
3. Input-driven actions route through command/session routines.
4. Deferred/cycle actions route through status or polling routines.
5. Timed events route through host timer/callback facilities.
6. Client/modem receives output after host/module processing.

Evidence state: **Derived**.

## Phase 2 — World Loop Reconstruction

Created:

- `Librarium/HostExecutionModel/world_loop_reconstruction_0078.json`

Derived loop order:

1. Accept/collect user input.
2. Process global commands.
3. Process enter/login/module transitions.
4. Process in-module command routine.
5. Process deferred/status routine.
6. Process optional per-channel polling.
7. Mark channel data complete.
8. Process timed/global events: `RTKICK`, `RTIHDLR`, `SYSCYC`, `INITASK`.
9. Dequeue pending statuses.
10. Deliver visible output through the session layer.

The first nine steps are strongly supported by MBBSEmu host code. Step ten is a derived projection that still needs output flush tracing.

## Phase 3 — Behavior Timelines

Created:

- `Librarium/BehaviorTimelines/subsystem_behavior_timelines_0078.json`

Timelines were created for:

- Combat
- Regeneration and timing
- Poison/disease/timed effects
- NPC AI
- Loot and spawn
- XP and leveling
- Quest runtime
- Output flush / player visibility

Each timeline now records:

- Candidate host cadence
- Likely entry point class
- Remaining unknowns
- Promotion requirement

## Gap Registry Additions

Created:

- `Librarium/GapRegistry/host_timing_gaps_0078.json`

New critical gaps:

1. `GAP-0078-TICK-SOURCE` — exact Classic MajorMUD tick source.
2. `GAP-0078-OUTPUT-FLUSH` — output flush boundary.
3. `GAP-0078-SUBSYSTEM-CALLBACK-MAP` — subsystem-to-callback mapping.

These gaps should drive the next verification package.

## What was not promoted

No behavior was promoted to Verified because MBBSEmu proves the emulated MajorBBS host substrate, not by itself the exact Classic MajorMUD module behavior.

Promotion requires one of:

- MajorMUD-specific routine mapping.
- Runtime trace showing routine/cadence/order.
- Corroboration from Reference Pack or observed Classic behavior.
- Deterministic simulator probes tied to the reconstructed host loop.

## Recommended next update

**CodeUpdate 0079 — Callback Mapping and Timing Probe Execution**

Objectives:

1. Map MajorMUD subsystems to host callbacks.
2. Build execution traces for combat, regen, AI, spawn, and quests.
3. Run timing probes using the reconstructed world loop.
4. Determine which timing gaps can be promoted from Hypothesis/Derived to Observed or Verified.
