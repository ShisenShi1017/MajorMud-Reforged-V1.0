# ServerSim0036 Cumulative Handoff

This folder preserves curated planning and regression evidence from `MajorMUD-Reforged-CumulativeHandoff-ServerSim-0036-WorkOnly.zip`.

It is evidence and planning material only. It does not modify canonical `Data/`, does not certify live Worldgroup runtime behavior, and does not enable Server gameplay.

## Useful Findings Folded Into CodeUpdate-0062

- MajorMUD remains a game module hosted by a reusable Server.
- `/Data` remains the runtime source of truth.
- Future scheduler work should start as registered diagnostic channels before any ticking or gameplay.
- ServerSim0036 suggested gameplay-facing scheduler concerns, but CodeUpdate-0063 keeps actual scheduler bootstrap channels generic to avoid hardcoding MajorMUD concepts into the reusable host.
- Combat appears scheduler-driven rather than simple command-immediate execution.
- XP credit should eventually be tracked per target/engagement set, not automatically by full party count.
- Effective XP should eventually use evidence-backed base XP, multiplier, and override rules.
- Cave worm and slime beast are the only currently identified stock instant-regen spawn cases.
- Player state should eventually support concurrent activity/status channels.
- Stealth/backstab should eventually be modeled as state channels, with broken stealth handled explicitly.
- Silver rapier blind proc is a future regression-test candidate.

## Boundary

CodeUpdate-0062 uses this handoff to justify a read-only scheduler bootstrap diagnostics surface only.

No combat service, spawn manager, movement execution, status-effect ticking, AI, XP award logic, world loop, socket/session handling, or gameplay behavior is implemented from this handoff.
