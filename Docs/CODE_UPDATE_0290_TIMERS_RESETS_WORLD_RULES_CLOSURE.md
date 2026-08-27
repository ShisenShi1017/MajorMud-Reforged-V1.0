# Code Update 0290 — Pass 20 Timers / Resets / World Rules Closure

Pass 20 continues the MajorMUD 1.11p parity program without inventing native timer, reset, or persistence semantics. Direct native WCCMMUD/WCCMMHLP evidence is separated from strongly correlated external tooling evidence and from unresolved reset/world-clock behavior.

## Native player-save interval

The retained native `WCCMMUD.mcv` catalog contains the exact configuration prompt:

`Seconds between saving Player data? 60`

The Classic runtime previously scheduled character autosaves every five minutes. That Reforged placeholder is removed. `RuntimeClassicTimingAuthority.PlayerDataSaveIntervalSeconds` is now `60`, and `ServerRuntimeHost` uses that 60-second interval for Classic character autosave scheduling.

The same native catalog also contains:

`Seconds between saving Buffer data? 300`

The 300-second value is retained as native evidence, but Pass 20 does **not** label a generic Reforged world-state store as native MajorMUD `Buffer data`. Exact buffer participants, dirty semantics, save ordering, shutdown behavior, crash recovery, and rollback remain UNKNOWN.

## NPC respawn admission

The retained native binary correlation includes:

- `Room Max: %d  Current: %d  Last Killed: %s Delay: %d`
- `Minutes before regenerating: %d minutes`

Nightmare Redux tooling further documents that resetting monster `Last Killed` makes those monsters regenerate the next time someone walks into the room. This is strong evidence, not direct native execution proof, so the behavior is activated as **STRONG** rather than PASS.

The live Classic path now behaves accordingly:

1. NPC death records an elapsed-time eligibility deadline.
2. The global living-world heartbeat no longer materializes due NPCs into empty rooms.
3. A successful player room entry asks the living-world service to admit due respawns for that exact home room.
4. Party followers that successfully enter the destination room use the same admission path before their room output is rendered.

Still UNKNOWN:

- the exact native wall-clock minute boundary/phase;
- zero `Delay` semantics;
- restart and missed-timer reconciliation;
- exact simultaneous due-spawn ordering;
- interaction with native world/area reset processing.

## REST / MEDITATE interruption repair

Native HELP states that REST and MEDITATE require at least 20 seconds before the increased regeneration takes effect, and that moving, attacking, or being attacked during that period terminates the state and yields no bonus.

The runtime already carried the 20-second activation delay but was over-broadly cancelling the state for almost every command. Pass 20 corrects this:

- ordinary non-movement commands do not cancel rest/meditation;
- movement cancels focused regeneration;
- actual PvE/PvP combat-entry paths cancel the attacker/defender focused state.

The separate resting HP and meditation MP tick cadences remain STRONG where their exact native phase is not proven.

## Graceful exit timing

The existing five-second graceful exit/save behavior remains PASS. Its constant is centralized in `RuntimeClassicTimingAuthority` so the native timing values are no longer scattered across unrelated policy classes.

## World reset boundary

Pass 20 does not promote the generic world-reset shell to native MajorMUD truth. Exact native reset categories/order, hard-reset exception encodings, active-player/combat behavior, corpse and limited-item survival, subsystem reset ordering, rollback, restart behavior, events, and broader world-rule configuration remain UNKNOWN.

## Parity result

Pass 19 baseline:

- PASS 71
- STRONG 69
- UNKNOWN 44
- Total 184
- Strict 38.6%
- Evidence-backed 76.1%

Pass 20:

- PASS 72
- STRONG 70
- UNKNOWN 44
- Total 186
- Strict 38.7%
- Evidence-backed 76.3%

New independently auditable requirements:

- `timers.player-data-save-60s` — PASS
- `timers.room-entry-respawn-admission` — STRONG

No broad UNKNOWN reset/world-clock requirement is falsely promoted.
