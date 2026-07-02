# Validation0012 — Field-Level Reverse Mapping Pass

## Objective
Validation0012 continues from Validation0011 and narrows the remaining blockers from subsystem-level validation to field/offset-level reverse mapping.

Primary targets:

1. Cleanup flag encoding: destroy/delete vs hide/searchable vs persistent.
2. Quest persistence fields: player/Btrieve state and quest gating.
3. Candidate field/offset inventory suitable for manual reverse-engineering follow-up.

## Sources Used

- Current validation cumulative baseline: Validation0011.
- Reconstructed `/Data` JSON records.
- MajorMUD disassembly source available from the uploaded MBBSEmu.Disassemblies package.
- Prior Validation0001–0011 reports and evidence.

## Statistics

- MajorMUD procedures detected in disassembly: 417
- Relevant procedures by name filter: 146
- Runtime data field inventory rows: 168
- Candidate offset/reference tokens: 67
- Quest/ASK/BTURNO signal lines: 1217
- Btrieve/BTU/BTECH signal lines: 1215
- Cleanup/hide/delete signal lines: 433
- Search command/hidden discovery signal lines: 83
- Monster/timer/generation signal lines: 325

## Findings

### Quest/Btrieve field mapping
Validation0012 found quest-gating and Btrieve-adjacent signals, including references around quest text, BTURNO checks, and ASK/monster interaction paths. This supports the existing model that some quest-like behavior is handled through command/routine logic and persistent state rather than static room data alone.

However, the pass did **not** prove exact quest field mappings. The evidence is sufficient to define candidate reverse-mapping targets, but not sufficient to write exact fields into `/Data`.

Current classification:

- Quest/persistence architecture: validated in engine docs.
- Quest field offsets: candidate-only.
- `/Data` quest promotions: not approved.

### Cleanup flag encoding
Validation0012 found cleanup/search/hidden signal paths and confirmed that the currently reconstructed `/Data` exposes `roomItems`, `hiddenItemIds`, and item lifecycle-style fields such as `deathDrop.destroyOnDeath`. It did **not** prove that any current field is the exact runtime cleanup flag for daily reset deletion/hiding.

Current classification:

- Cleanup lifecycle architecture: validated in engine docs.
- Hidden/searchable object model: validated in engine docs.
- Destroy-on-cleanup / hide-on-cleanup field encoding: unresolved.
- `/Data` cleanup promotions: not approved.

### Candidate offset table
A conservative candidate offset/token table was produced for future manual tracing. These are not promoted facts; they are trace anchors.

Evidence files added:

- `Evidence/ExternalSources/Validation0012/field_inventory_from_runtime_data.csv`
- `Evidence/ExternalSources/Validation0012/major_mud_relevant_procedures.csv`
- `Evidence/ExternalSources/Validation0012/candidate_offset_tokens.csv`
- `Evidence/ExternalSources/Validation0012/quest_btrieve_snippets.json`
- `Evidence/ExternalSources/Validation0012/cleanup_hidden_snippets.json`
- `Evidence/ExternalSources/Validation0012/field_offset_snippets.json`

## Promotion Review

### Approved engine-documentation promotion

- Validation0012 promotes the existence of a field-level reverse-mapping target set for quest/Btrieve state and cleanup hidden/delete state.

### Not approved

- `/Data` promotions: 0
- Exact cleanup flags: not promoted
- Exact quest persistence fields: not promoted
- Exact daily cleanup item state transition flags: not promoted

## Remaining Unknowns

- Which exact original record/offset marks destroy-on-cleanup.
- Which exact original record/offset marks hide-on-cleanup.
- Whether cleanup flags are item-level, room-item-level, runtime-state-level, or mixed.
- Which exact persistent record stores quest progress.
- Whether quest state is stored only per-player or partly world/global.

## Next Pass

Validation0013 should pick one concrete path and trace it manually:

- Either the quest-gated routine around the `complete a quest` message,
- or the cleanup/hidden-object path linked to `CLEAR_SEARCH_FLAGS` and hidden item state.

The next pass should avoid broad scanning and produce a small call-chain/offset map with confidence labels.
