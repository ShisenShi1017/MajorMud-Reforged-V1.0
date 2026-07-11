# AI Context for Reforged

Read `AGENTS.md` first, then read this file, then read `Docs/PROJECT_STATUS.md`.

`Docs/PROJECT_STATUS.md` is the concise live handoff. Old duplicate CodeUpdate handoff/changelog/manifest doc trees were removed to keep the repository readable.

## Current Position

- Current Code Update: `0137`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0137 - Complete Classic Gameplay`.
- Current local status: complete and verified locally.
- Latest local complete runtime verification: PASS 76/76.
- Latest report: `Reports/RuntimeVerification/complete_runtime_verification_20260711T143632Z.md`.
- GitHub Actions is pending until this commit is pushed and the current pushed commit is reported green.

## Non-Negotiable Rules

- Librarium is the authoritative gameplay source.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Preserve `Saves/` and `ImportedDatabases/`.
- Do not commit smoke-save artifacts.
- Do not move to the next public update until CodeUpdate0137 is pushed and GitHub Actions is green, unless the user explicitly overrides the gate.

## CodeUpdate0137 Summary

- Character Foundation generation and runtime smoke are complete.
- 13 races, 15 classes, and 195/195 race/class combinations are generated and verified.
- New characters start at `room.2147`, `Newhaven, Adventurer's Guild`.
- Items and equipment generation/runtime smoke are complete.
- Classic bug-fix contracts remain enforced, including mask face-slot correction, Lyrist's Companion spell binding, bard 10% shop minimum, cloaked figure XP, and drop-roll uniformity.
- Runtime slices for spells, spell execution, combat, actors/abilities, interaction/narrative, services/economy, world/multiplayer, generated data, rooms/exits, NPC loading, local session, compiler tests, and core tests pass in the complete gate.
- Active documentation was consolidated into the root docs/manifests plus `Docs/PROJECT_STATUS.md` and `Docs/Architecture/PROJECT_ROADMAP.md`.

## Next Gate

1. Commit and push CodeUpdate0137.
2. Wait for GitHub Actions green on the pushed commit.
3. Only then start the next approved update.

## Startup Order

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/PROJECT_STATUS.md`
4. `Docs/Architecture/PROJECT_ROADMAP.md`
5. `README.md`
6. `VERSION.md`
7. `CHANGELOG.md`
8. `ProjectManifest.json`
9. `DEVELOPMENT_PRINCIPLES.md`
10. Relevant source, generated data, validation, and report files
