# AI Context for Reforged

Read `AGENTS.md` first, then read this file, then read `Docs/PROJECT_STATUS.md`.

`Docs/PROJECT_STATUS.md` is the concise live handoff. Old duplicate CodeUpdate handoff/changelog/manifest doc trees were removed to keep the repository readable.

## Current Position

- Current Code Update: `0138`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0138 - Server Runtime and Host Enablement`.
- Current local status: closed and verified locally.
- Latest local complete runtime verification: PASS 55/55.
- Latest report: `Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md`.
- GitHub Actions is pending until this commit is pushed and the current pushed commit is reported green.

## Non-Negotiable Rules

- Librarium is the authoritative gameplay source.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Preserve real `Saves/` and `ImportedDatabases/`.
- Do not commit smoke-save artifacts.
- Do not move to the next public update until CodeUpdate0138 is pushed and GitHub Actions is green, unless the user explicitly overrides the gate.

## CodeUpdate0138 Summary

- Server runtime and host enablement are integrated.
- Host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audits are present.
- Live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world-system runtime smokes are covered.
- Generated Classic data compiles from Librarium.
- MMExplorer movement evidence is promoted through Librarium records and generated `/Data`.
- MMExplorer textblock quest-flow fix 0142 is promoted into Librarium quests.
- Quest records include 83 textblock quest-flow records and 2,183 total quest records.
- Active documentation is consolidated into the root docs/manifests plus `Docs/PROJECT_STATUS.md` and `Docs/Architecture/PROJECT_ROADMAP.md`.

## Next Gate

1. Commit and push CodeUpdate0138.
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
