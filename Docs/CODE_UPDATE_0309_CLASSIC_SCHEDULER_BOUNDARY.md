# Code Update 0309 — Classic Scheduler Boundary (Pass 38)

Pass 38 verifies the supplied phase-order document against retained MajorMUD evidence instead of implementing it literally.

## Result

`native.clock-scheduling`: **UNKNOWN -> STRONG**.

The Classic runtime already has independently recovered owner clocks: **5s combat**, **3s spell/system**, **60s player save**, **300s Buffer-data setup interval**, **5s EXIT delay**, **20s focus activation**, plus STRONG **30/20/10s passive/REST/MEDITATE** regeneration and STRONG room-entry admission of due minute-delayed NPC/lair respawns.

Pass 38 centralizes the cadence constants used by combat/status/regeneration so duplicate literals cannot silently drift. It does **not** invent one global scheduler phase.

## Rejected as a universal Classic model

The supplied document's universal 10-second Phase 4 is not supported as the owner of all DOT, regeneration and buff/debuff timing. Ten seconds is a supported MEDITATE cadence, but other native owners use distinct clocks.

Player-first NPC ordering, poison-before-regeneration same-cycle priority, exact corpse/removal/spawn cleanup order and a mandatory next-tick delay for newly spawned NPC actions remain evidence-gated.

## Remaining boundary

Exact Worldgroup host phase, simultaneous cross-owner event order, native background bucket cadence, restart/missed-timer reconciliation, overflow/drift, and NPC/player/spawn same-cycle ordering remain unresolved. This prevents PASS.

Parity after Pass 38: **97 PASS / 79 STRONG / 24 UNKNOWN = 200**; strict **48.5%**, evidence-backed **88.0%**.
