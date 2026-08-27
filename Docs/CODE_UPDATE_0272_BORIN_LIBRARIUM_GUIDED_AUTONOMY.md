# CodeUpdate0272 — Borin Librarium-Guided Autonomy

This cumulative update extends CodeUpdate0271.

## Corrected implementation
- Borin's autonomous player decision loop now consults catalog-listed structured records under `Librarium/Objects` instead of relying on a generic text scan.
- The earlier draft imposed a 2 MB file ceiling that silently excluded the primary NPC, item, spell, quest, spawn, shop, and room record files. That ceiling has been removed for canonical catalog-listed records; files up to 64 MB are accepted.
- Only record files explicitly registered by each Librarium family `catalog.json` are indexed. SQL/MDB reference extracts and arbitrary repository text are not treated as player authority.
- The advisor caches a compact parsed index and invalidates it when the canonical record files change, so Borin does not re-read multi-megabyte Librarium files every game action.

## Autonomous leveling guidance
- Verified live character level and experience are supplied to Borin's decision prompt.
- Borin receives conservative leveling candidates from NPC records near his verified level.
- NPC level, XP, HP, spawn room, and respawn data are linked across Librarium NPC/spawn/room records when available.
- Current-room spawn context is supplied when the live room identity can be matched to Librarium room/spawn references.
- `STATS` and `EXP` are included among normal player information-command examples so Borin can refresh missing progression evidence himself.
- Leveling candidates are explicitly planning guidance, not guarantees of safety or optimal XP.

## Authority boundaries
- Live terminal evidence remains authoritative for current room, exits, HP, combat state, inventory, equipment, and command results.
- Librarium records labeled `PROVEN` may be used directly as reference knowledge.
- `STRONGLY CORRELATED` records may guide a cautious plan and must be verified through gameplay.
- Other confidence labels remain reference clues and are not promoted automatically.
- Librarium access is read-only; autonomous gameplay cannot mutate Librarium data.

## Deterministic regression coverage
- The Borin character harness now creates a catalog-listed NPC source deliberately larger than 2 MB and verifies that the advisor still reads it.
- The test verifies that a level-matched NPC is connected to its spawn room and XP evidence.

## Packaging
The cumulative ZIP contains only files introduced or changed by CodeUpdate0271 and CodeUpdate0272, preserving repository-relative paths under the repository top folder. Unchanged repository files are excluded and each file appears once.

## Verification
The preparation environment does not contain the .NET SDK, so a local Reforged build/test run is still required after applying the ZIP. The source and package were structurally audited here, including JSON validation, changed-file comparison, and deterministic inspection of the Librarium data paths used by the advisor.
