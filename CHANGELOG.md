# Changelog

## CodeUpdate-0137 - Complete Classic Gameplay

CodeUpdate0137 is complete locally and ready for commit/push.

### Character Foundation

- Generates 13 playable races and 15 playable classes from Librarium-backed records.
- Accounts for all 195 race/class combinations.
- Promotes generated race stats, class HP/mana-related fields, class/race ability slots, and Newhaven character start data.
- Runtime character creation smoke creates, saves, and reloads at `room.2147`, `Newhaven, Adventurer's Guild`.

### Items, Equipment, and Classic Repairs

- Adds generated Classic item/equipment projections.
- Adds runtime item/equipment engine coverage for inventory, equipment, containers, corpses, loot, validation, duplicate rollback, snapshot/reconnect, and world-state save/load.
- Folds evidence-backed Classic bug-fix contracts into generated item data, including mask face-slot correction and Lyrist's Companion `spell.1055`.
- Keeps bard 10% shop minimum, cloaked figure 10,000 XP, no `room.unknown`, duplicate trigger canonicalization, drop-roll uniformity, and runtime correction contracts under verification.

### Runtime Verification

- Adds the expanded complete runtime verification gate for all current 0137 validators and server smoke modes.
- Generated data, room/exit, NPC, spell, spell-execution, combat, combat-runtime, actors/abilities, interaction/narrative, services/economy, world/multiplayer, and scripted local-session smokes pass.
- Compiler tests, core tests, solution restore/build, Python validators, and simulator validators pass.
- Latest local complete runtime verification passed 76/76:
  `Reports/RuntimeVerification/complete_runtime_verification_20260711T143632Z.md`

### Documentation Cleanup

- Consolidates active continuity into the root docs/manifests plus [Docs/PROJECT_STATUS.md](Docs/PROJECT_STATUS.md) and [Docs/Architecture/PROJECT_ROADMAP.md](Docs/Architecture/PROJECT_ROADMAP.md).
- Removes duplicate historical handoff, changelog, manifest, and old planning doc trees to reduce repository clutter.

GitHub Actions remains pending until this commit is pushed and the current pushed commit is confirmed green.
