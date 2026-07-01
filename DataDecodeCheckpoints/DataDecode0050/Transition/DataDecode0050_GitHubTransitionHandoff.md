# DataDecode0050 - GitHub Transition Handoff

## Objective

Stop using checkpoint ZIPs as independent working repositories.

Beginning with the next decode pass, the GitHub repository is the single authoritative working copy for decode work. ZIPs may still be exported as archival or release artifacts, but they are no longer the primary source of truth.

Repository:

`ShisenShi1017/MajorMud-Reforged-V1.0`

## Current Decode Status

User-provided continuity reports DataDecode0001 through DataDecode0050 as the current handoff sequence.

Current confidence estimates from the handoff:

- Static binary reverse engineering: approximately 98%.
- Record structures: approximately 99.7%.
- Room graph: approximately 99.5%.
- Items: approximately 98%.
- Spells: approximately 98%.
- Monsters/NPCs: approximately 97-98%.
- Shops: approximately 98%.
- Messages/Textblocks: approximately 98%.
- Quest structures: approximately 97%.
- Cross-database relationships: approximately 98%.
- WGSERVER executable correlation: approximately 96%.

## Authority Order

1. Original MajorMUD binaries.
2. `WGSERVER.EXE`.
3. Native DAT files.
4. GitHub repository.
5. WBB comparison files.
6. Imported datasets.
7. HTTrack OmniVerse mirror, verification only.

Binary evidence always wins. OmniVerse cannot override binary evidence, and OmniVerse IDs must never be assumed to equal binary record indexes.

## Completed Decode Themes

- Rooms.
- Room graph.
- Room transitions.
- Items.
- Spells.
- Shops.
- Monsters/NPCs.
- Messages.
- Textblocks.
- Quest structures.
- Ability references.
- Foreign-key reconstruction.
- Canonical schemas.
- Import readiness.

## Executable Correlation Status

DataDecode0048 covered WGSERVER loader correlation:

- PE analysis.
- Section inventory.
- Import table.
- String catalog.
- Loader candidate windows.
- DAT filename references.

DataDecode0049 covered WGSERVER record access / structure correlation:

- Record descriptor candidates.
- Record access windows.
- Record size constants.
- Canonical schema correlation.

DataDecode0050 is recorded as a transition handoff for event/message dispatcher correlation. No standalone DataDecode0050 ZIP was supplied in this Codex turn. The user-provided handoff lists the following 0050 work as candidate continuity:

- Event dispatcher candidate regions.
- Message dispatcher candidate regions.
- Textblock anchors.
- WCCMSG/WCCTEXT references.
- Quest-event bridge candidates.
- Dispatcher priority report.

These 0050 items remain handoff continuity until supported by committed repository reports or binary-backed evidence. No noisy fields are promoted by this transition.

## Repository Workflow

Future decode passes must update the repository directly:

1. Start from the current GitHub repository state.
2. Preserve previous reports instead of overwriting historical analysis.
3. Commit each decode pass.
4. Keep continuity documents synchronized.
5. Keep canonical schemas synchronized when a reviewed binary-backed change is approved.
6. Keep reports, metrics, and handoff documentation synchronized.
7. Treat ZIPs only as archival/checkpoint exports, not working sources of truth.

## Standing Rules

- Binary evidence always wins.
- No guessing.
- No speculative field promotion.
- No promotion based solely on U16/U32 matches.
- Every promoted field requires binary evidence.
- Imported/reference data remains evidence only until a reviewed reconciliation update changes canonical `Data/`.

## Remaining Decode Work

Next planned passes:

- DataDecode0051 - Combat Routine Correlation.
- DataDecode0052 - Economy Routine Correlation.
- DataDecode0053 - Quest / Flag Engine Correlation.
- DataDecode0054 - Final Executable to Canonical Schema Audit.

Goal: move from approximately 98% static confidence toward approximately 99% by proving executable usage of remaining candidate fields rather than discovering additional structures.
