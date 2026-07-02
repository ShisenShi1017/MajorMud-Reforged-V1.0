# DataSimVer0024 Documentation — Quest/Boss Runtime Hypotheses

DataSimVer0024 keeps the reconstruction evidence-first.

The current best model is that MajorMUD/Worldgroup runtime behavior likely separated persistent world cleanup, normal respawns, explicit death timers, and quest state. The separation is useful for simulation, but it is not yet executable-confirmed.

## Current Hypothesis
- Normal monsters: checked on an approximate 15-minute world respawn cycle.
- Explicit timer monsters: countdown begins on death and continues through reset.
- Quest monsters: may be gated by player or world state.
- Boss/unique monsters: may be gated by uniqueness, cooldown, or quest/event state.
- Cleanup: daily reset may destroy some items and hide others in their current room.
- Search: hidden items remain recoverable through stat-influenced search.

## Evidence Status
All items above are documented or user-provided runtime knowledge pending reverse-engineering verification. `/Data` remains unchanged.
