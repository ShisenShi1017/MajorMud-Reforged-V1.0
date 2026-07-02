# Validation0010 — Field-Level Reverse Mapping

## Objective
Trace implementation-level fields and algorithms for the remaining blockers: Search formula, hidden-exit discovery fields, monster generation timer gates, cleanup flag encoding, and quest persistence records.

## Baseline
- Trusted validation baseline: Validation0009 rerun package.
- External evidence: MBBSEmu source, MBBSEmu.Disassemblies MajorMUD `WCCMMUD.DLL.asm`, MDB extractor tool.
- `/Data` policy: unchanged unless direct, field-level evidence supports a data promotion.

## What was validated

### Search / hidden exits
Validation0010 recovered the most precise Search implementation evidence so far. `_CMD_SEARCH` dispatches the command, blocks searching during autocombat, displays room items for area search, parses directional search targets, calls `_SEARCH_FOR_HIDDEN_EXITS`, and adds delay. `_SEARCH_FOR_HIDDEN_EXITS` confirms a dedicated hidden-exit/trap routine.

Recovered implementation-level facts:
- Directional hidden exits are represented by a room exit type check against value `6` at room offset `+360h + direction*2`.
- The hidden-exit flag check uses bit `0x02` at room exit flag offset `+374h + direction*4`.
- Discovery rolls `GENRDN(0, 100)` and compares the roll against a player-derived value from player offset `+5EDh`, adjusted by `-15`, with a minimum threshold of `3`.
- On success, the room state is mutated: room dirty/touched byte `+5EDh` is set, exit auxiliary word `+376h + direction*4` is cleared, and exit flag word `+374h + direction*4` is set to `4`; `_ADD_EXIT_TO_LIST` is called.
- Trap search uses exit type `9`, rolls `GENRDN(0,100)`, and compares against player offset `+5FBh`.
- Search incurs `_ADD_DELAY`.

Promotion result: **engine documentation promotion approved** for exact hidden-exit Search control flow and offset-level algorithm. `/Data` promotion remains blocked because offsets have not yet been mapped to canonical JSON field names or original MDB column names.

### Monster generation / respawn timers
`_GENERATE_MONSTER` was traced deeper. The routine includes room-capacity gates, room monster-slot occupancy gates, time gates, relocation/linked-room checks, unique/active monster allocation, and a timed elapsed gate for monster definitions with fields at offsets `+0A1h`, `+0A3h`, `+0ADh`, `+0AEh`, and `+0B0h`.

Recovered implementation-level facts:
- Room occupancy/capacity gate compares active room count-like field `+5F7h` against capacity-like field `+554h`.
- Monster room slot occupancy is checked through 15 four-byte slots at room offsets beginning near `+400h`.
- A room time gate uses room offset `+559h` and elapsed-time logic involving `NOW`, a conversion routine, `word_8D9A2`, `word_99924`, and optional room offset `+5B3h`.
- Known monster timed generation uses fields around `+0A1h`/`+0A3h` as active/current count gates, and fields `+0ADh`/`+0AEh`/`+0B0h` with `COFDAT`, `NOW`, `_CALC_MINUTES_DIFFERENCE`, and `LNGRND` to test elapsed-time windows.
- `_GET_UNIQUE_ACTIVE_MONSTER_NUMBER` is part of the generation path, supporting active-instance allocation for generated monsters.

Promotion result: **engine documentation promotion approved** for offset-level respawn/generation gates. Exact 15-minute standard respawn interval remains not promoted because the traced routine shows timed gates but does not by itself prove the global interval constant. `/Data` promotion remains blocked pending field-name mapping.

### Cleanup flags
Validation0010 did not identify exact item cleanup flag encoding. Cleanup lifecycle remains engine-documented from prior passes, but destroy-on-cleanup vs hide-on-cleanup cannot yet be written to `/Data`.

Promotion result: **no new cleanup flag promotion**.

### Quest persistence
Quest evidence remains strongest around action/experience routines such as `_ADD_QUEST_EXP`, and question/response paths such as `_ASK_MONSTER_A_QUESTION`, but exact Btrieve record fields for quest persistence were not fully mapped in this pass.

Promotion result: **no quest `/Data` promotion**.

## Statistics
- MajorMUD disassembly lines scanned: 328626
- `_CMD_SEARCH` lines traced: 197
- `_SEARCH_FOR_HIDDEN_EXITS` lines traced: 315
- `_GENERATE_MONSTER` lines traced: 1621
- `_ADD_QUEST_EXP` lines traced: 150
- Hidden-exit random calls in traced routine: 2
- Search delay calls in command routine: 2
- Monster generation room-data calls: 3
- Monster generation NOW calls: 4
- Monster generation elapsed-minute comparison calls: 1
- Monster generation random long calls: 6
- Quest keyword hits in disassembly: 144
- MBBSEmu database/Btrieve-like hits: 3163

## Changes
- Added offset-level Search/hidden-exit validation report.
- Added offset-level monster generation/timer gate validation report.
- Added evidence CSV for line-level reverse-mapping clues.
- Updated EngineKnowledge with newly validated engine-documentation promotions.
- `/Data` unchanged.

## Remaining unknowns
- Canonical mapping from player offset `+5EDh` and `+5FBh` to named stats/derived stats.
- Canonical mapping from room offsets `+360h`, `+374h`, `+376h`, `+5EDh`, `+559h`, `+5B3h`, `+5F7h`, `+554h`, and `+400h` slot array to JSON/MDB fields.
- Canonical mapping from monster offsets `+0A1h`, `+0A3h`, `+0ADh`, `+0AEh`, and `+0B0h` to named timer/count fields.
- Exact cleanup item flag encoding.
- Exact quest persistence Btrieve fields.

## Promotion review
Engine/documentation promotions approved: **2**
1. Exact Search/hidden-exit control flow and offset-level formula.
2. Monster generation/respawn offset-level gate architecture.

`/Data` promotions approved: **0**

## Next pass
Validation0011 should focus on struct/field-name mapping: resolve disassembly offsets to extracted MDB fields and current `/Data` JSON fields.
