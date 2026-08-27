# Classic Scheduler Boundary — Pass 38 / Update 0309

Classification: **STRONG owner-scoped Classic scheduler boundary**.

Pass 38 reviews the user-provided five-phase scheduler page as a hypothesis, then checks it against retained Classic evidence. The page is **not** treated as native documentation.

## What is already closed at an owner boundary

- Physical combat: **5 seconds** — PASS.
- Spell/status duration and periodic processing: **3 seconds** — PASS.
- Player-data autosave: **60 seconds** — PASS from exact `WCCMMUD.mcv`.
- Native Buffer-data setup interval: **300 seconds** — exact constant, while participant ownership/order remains unknown.
- EXIT/QUIT delay: **5 seconds** — PASS from native HELP.
- REST/MEDITATE activation: **20 uninterrupted seconds** — PASS from native HELP.
- Passive/REST/MEDITATE regeneration: **30 / 20 / 10 seconds** — STRONG recovered cadence evidence.
- Lair/NPC regeneration: STRONG evidence says the authored delay is minute-based and a due spawn is admitted when a player enters the home room; the live global heartbeat does not materialize it into an empty room.

These are heterogeneous owner clocks. They must **not** be collapsed into a universal ten-second gameplay phase.

## Review of the supplied phase document

The proposed universal Phase 4 ten-second tick is unsupported as a Classic-wide scheduler model. A ten-second cadence is strongly recovered for focused MEDITATE, but combat, spell/status, REST, passive regeneration, saves, EXIT and room-entry respawn have different owner boundaries.

The following remain hypotheses rather than parity facts: player-first/NPC-second global ordering; poison-before-regeneration same-cycle priority; global ten-second buff/debuff decrement; exact corpse/removal/spawn cleanup order; and a rule forbidding newly spawned NPCs from acting until the next native host cycle.

## Why the broad bucket can become STRONG

`native.clock-scheduling` previously grouped all timing behind UNKNOWN even though several independent owner clocks are already PASS/STRONG and runtime-connected. Pass 38 narrows the bucket: the known owner clocks now receive evidence-backed credit, while exact host phase, same-cycle cross-owner ordering, background-bucket cadence, restart/missed-timer behavior, drift/overflow, NPC/player ordering, and spawn-action timing remain explicitly unresolved.

The assertion therefore moves **UNKNOWN -> STRONG**, not PASS.
