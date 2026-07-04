# CodeUpdate-0058 - World Projection Index

## Summary

CodeUpdate-0058 closes the Server `world_rooms` projection gap without rewriting canonical `Data/`.

The Core data loader now supports string-array collections as scalar canonical records. This lets the existing `world_rooms` descriptor load `Data/world.json` `rooms[]` entries as stable ids while preserving existing object-record loading behavior.

The Server bootstrap also now attaches a read-only world projection audit that correlates `Data/world.json` against `Data/rooms.json`.

## Changed

- Added scalar string-array support to `CoreDataRepositoryLoader`.
- Added `ServerWorldProjectionAudit`.
- Added `ServerWorldProjectionAuditReport`.
- Attached `world-projection` to Server bootstrap.
- Printed world projection status in Server startup output.
- Added smoke coverage for the world projection and bootstrap attachment.

## Verified Projection

- World room ids: 1,246.
- Unique world room ids: 1,246.
- Canonical room records: 1,246.
- Missing world room references: 0.
- Duplicate world room ids: 0.
- Start room: `wcc_map1_room1`.
- Start room exists in canonical rooms: true.

## Boundaries

- No canonical `Data/` rewrite.
- No imported/reference deletion.
- No gameplay system.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No live editor behavior.
- No runtime formula or timing promotion.

## Verification

- ProjectManifest and UpdateManifest JSON parse passed.
- `git diff --check` passed.
- Sequential Release builds for Core, Server, Importer, Editor, Launcher, Client, and Core.Tests passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.
- Server bootstrap passed and reported CodeUpdate 0058, validation 0 issues, `world-projection`, `world_rooms` records=1,246, runtime-ready categories=12, runtime-blocked categories=1, placeholder categories=1, and total loaded records=8,189.
