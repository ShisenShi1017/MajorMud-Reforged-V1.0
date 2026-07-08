# Librarium-0004 — NPC/Textblock Quest Flow Reconstruction

This pass expands the Librarium graph toward quest-flow reconstruction without promoting unproven quest records into `Data/quests.json`.

## What changed

- Added raw textblock reference nodes from MMExplorer MDB owner table rows.
- Added raw owner-row evidence nodes where textblock references or quest terms are visible.
- Added canonical text evidence nodes for entity descriptions that mention quest/ability terms.
- Added per-quest flow candidate nodes for quest ability flags.
- Connected GoodQuest candidate flow to the observed White Gold Ring reward chain while preserving its evidence state.

## Guardrail

Sysop/editor command strings are not treated as runtime proof. They are not used to promote quest flow.

## Current counts

- Nodes: 14480
- Relationships: 39728
- Quest flow candidates: 12
- Textblock references observed: 16
- Unique textblocks referenced: 12
- Canonical text mentions: 16
- Raw owner-row term mentions: 3656

## Promotion status

`Data/quests.json` was not changed. Flow candidates must be promoted later only after decoded NPC/textblock ordering and/or runtime observation supports exact stages.
