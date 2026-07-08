# Runtime-0012 — Core Runtime Kernel, Command Processor, and Game Loop

Runtime-0012 converts the runtime from isolated calculators into an offline interactive kernel.

## Added in this milestone

- Runtime world state object for players, monsters, NPCs, ground items, corpses, combat pairs, status effects, timers, events, and runtime flags.
- Command parser with alias handling and quoted-argument support.
- Command registry and dispatcher.
- Core commands: look, movement, inventory, equipment, get, drop, attack, cast, say, whisper/tell, who, help, buy hook, deposit hook.
- Event queue with scheduled/due event processing.
- Tick scheduler with independent task intervals and priorities.
- Runtime kernel that connects command dispatch, movement, combat, spells/statuses, regeneration, monster AI hooks, spawn hooks, NPC service hooks, and cleanup hooks.
- Demo world builder and integration validation.

## Formula and accuracy policy

This milestone does not claim exact classic MajorMUD command timing, combat math, spell math, AI timing, or parser quirks. Runtime-0012 uses the existing formula provenance model and keeps Reforged behavior separated from classic-verified behavior.

## Validation

Run:

```bash
python Sim/run_verification_suite.py
```

Expected: all implemented validators PASS; the remaining placeholder validator reports UNKNOWN by design for persistence, network sessions, dedicated server integration, and final playable integration.

## Remaining runtime work

- Runtime-0013: persistence/save-load.
- Runtime-0014: network sessions and dedicated server integration.
- Runtime-0015: first fully playable local runtime build.
