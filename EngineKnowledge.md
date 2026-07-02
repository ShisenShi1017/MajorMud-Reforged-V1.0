# EngineKnowledge


## DataSimVer0011 — Action Resolution Expected Model

- The simulator now models common parser verbs separately from directional movement.
- Movement evidence is inherited from DataSimVer0010 traversal.
- `buy` is strongest when a room has a structured `shopNumber` binding.
- `open` and `unlock` are treated as expected-model hypotheses when room text, exit types, doors, gates, locks, or key clues are present.
- `talk`, `read`, `cast`, `train`, `push`, `pull`, `climb`, `crawl`, and `enter` are recorded as parser-target hypotheses when `/Data` exposes target clues.
- No action behavior was promoted into `/Data`.
- No special-exit semantics were promoted.


## DataSimVer0012 — Service/NPC Interaction Expected Model
- Service and NPC behavior is still treated as an expected model, not proof of original runtime behavior.
- Extracted shop/service, trainer, and spawn records support a table-driven interaction model.
- No `/Data` records were changed or promoted. Numeric shop/service type labels remain provisional until original engine or MBBSEmu evidence confirms semantics.


## DataSimVer0013 Custom Content Boundary

User-requested custom content was registered as overlays, not legacy `/Data`: a sysop/GM-only Synopsis Room and command `code_of_twinks_78`. Possible sysop/GM placeholder rooms are tracked as a hypothesis only until original engine or MBBSEmu evidence is available.


## DataSimVer0014 — Inventory/Economy Findings
- Real measured 0014 pass executed from the uploaded DataSimVer0013 cumulative baseline.
- Items analyzed: 1950; shops analyzed: 178; resolved shop inventory refs: 1204.
- Persistent inventory/economy state can be simulated from item/shop data fields without modifying `/Data`.
- Expected-model transaction successes: 3357; failures: 7099.
- Inferred mechanics such as sell-back percentage and carry limit remain unpromoted until runtime/MBBSEmu evidence confirms them.
- `/Data` unchanged; room endpoints promoted: 0; special-exit semantics promoted: 0.


## DataSimVer0015 — Combat/Status/Spell-State Notes
- Evidence source: Data/monsters.json and Data/spells.json from DataSimVer0014 cumulative package.
- Monster rows analyzed: 1100; attack rows identified: 2215.
- Spells analyzed: 1350; timed/status duration clues: 543.
- Combat formulas remain expected-model approximations until verified against original MajorMUD or MBBSEmu runtime.
- /Data was not changed; no endpoint or special-exit semantics were promoted.


## DataSimVer0016 — Monster Ecology / Spawn / Loot Expected Model
Rooms contain spawn records that can be mapped to monster legacy identifiers. The simulator can model expected reward/death rows from monster XP, currency, and loot tables, but exact runtime death, corpse, respawn, linked-spawn, and loot-roll semantics remain unverified. `/Data` was not changed.


## DataSimVer0016 Cleanup/Search Visibility Hypothesis

User-provided runtime hypothesis: cleanup can hide some room/items instead of deleting them. Hidden items may require the `search` command to discover, and search success is affected by player stats. This is recorded as an engine-behavior hypothesis only; `/Data` remains unchanged. Future simulator work should distinguish visible, cleanup-hidden-searchable, and cleanup-deleted/removed item states.


## DataSimVer0017 — Cleanup / Hidden Items / Search
The data model separates visible room items (`roomItems`) from hidden room items (`hiddenItemIds`). `skills.json` contains a `search` skill for finding hidden items and clues, governed by `agi` and `int`. This supports a three-state expected visibility model: visible, cleanup-hidden/searchable, and cleanup-deleted/removed. Exact cleanup timing, exact hide/delete rules, and exact search formula are not promoted without engine evidence. `/Data` unchanged.


## DataSimVer0018 Runtime Lifecycle Notes

Evidence-only scan found room respawnMinutes and monster respawnHours/regen fields. No global world reset schedule or cleanup deletion policy was promoted. Hidden item behavior remains documented only until runtime/reverse-engineered evidence confirms Search/stat mechanics.


## DataSimVer0019 Notes

Cleaned packaging to required top-level structure. Scheduler and cleanup matrices remain non-promoting hypothesis artifacts. Native message catalog matches are clue-only and not runtime semantics.


## DataSimVer0020 — Daily Worldgroup Cleanup / Reset Hypothesis

User hypothesis recorded: original Worldgroup likely performed a daily reset/maintenance cycle that ran cleanup. Expected model: destroy-on-cleanup items are deleted from runtime state; hide-on-cleanup items remain in the current room as hidden/searchable objects; Search uses the `search` skill governed by agi and int. This is documented only. `/Data` remains unchanged; no cleanup semantics were promoted.

## DataSimVer0021 — Monster Respawn Scheduler Hypothesis

User runtime knowledge recorded: normal creatures respawn on approximately a 15-minute global cycle. Monsters configured with an explicit respawn timer start that timer on death, and the timer continues through daily reset/cleanup rather than being restarted by reset. The simulator now distinguishes a global normal-creature respawn sweep from per-monster death-triggered countdown timers. This remains documented hypothesis / expected-model behavior only; `/Data` is unchanged and no monster respawn semantics were promoted.



## DataSimVer0022 — Monster Respawn Scheduler Split
User runtime knowledge added as documented hypothesis: normal creatures are believed to respawn on approximately a 15-minute cycle, while monsters with explicit respawn timers start their timer on death and that timer continues through daily reset/cleanup. This is not promoted into `/Data` until verified by reverse engineering or direct runtime evidence.

## DataSimVer0023 — Quest/Boss Respawn and Occupancy Hypothesis

The simulator now documents boss/unique monster and quest monster respawn as a separate uncertainty area. Boss or unique monsters should not automatically be treated as normal ~15-minute respawn creatures; they may use explicit timers, quest gates, or special persistence rules. Room occupancy / spawn-slot occupancy should be checked before creating a respawn duplicate, but the exact original rule is unknown. Cleanup flags and respawn flags remain separate hypotheses until direct engine evidence confirms the schema. `/Data` remains unchanged and no semantics were promoted.



## DataSimVer0024 — Quest/Boss Persistence and Occupancy Model

- Added documented-only separation of quest persistence, boss/unique respawn, room occupancy, cleanup, and respawn timers.
- User-provided monster timing model remains hypothesis pending reverse-engineering: normal creatures ~15 min global respawn; explicit timers start on death and continue through reset.
- No `/Data` promotion occurred in this pass.


# Validation0001 External Evidence Notes

Validation0001 added an external validation track using MBBSEmu, MBBSDASM, MBBSEmu.Disassemblies, and EditorPatch0003.

Host-level evidence from MBBSEmu supports a nightly cleanup scheduler with configurable Cleanup.Time, default 03:00 when missing/unparsable, daily timer period, session removal, module `mcurou` and `finrou` calls, configured cleanup EXE execution, and host restart.

MajorMUD module-level behavior remains unpromoted until WCCMMUD.DLL.asm routines are mapped. Search strings and timer references exist in the disassembly, but formulas and item/monster lifecycle routines are not yet validated.


## Validation0002 — MBBSEmu Host + MajorMUD Disassembly Mapping

Status: External evidence mapping; no `/Data` promotion.

Confirmed against MBBSEmu host source:
- Configurable `Cleanup.Time` exists for nightly cleanup.
- Host schedules cleanup daily with warning/grace behavior.
- Cleanup sequence calls module `mcurou`, module `finrou`, runs MDF cleanup programs, then restarts/reloads modules.
- Host tick timer exists and defaults to 36 Hz when enabled.

Mapped in MajorMUD disassembly:
- Background routines include _BACKGROUND_ENERGY, _BACKGROUND_FAST, _BACKGROUND_INACTIVITY, _BACKGROUND_MEDIUM, _BACKGROUND_MONSTER_CREATE, _BACKGROUND_OTHER, _BACKGROUND_SAVE_BUFFERS, _BACKGROUND_SLOW, _BACKGROUND_UPDATE_EXITS.
- `RTKICK` references: 44.
- `BEGIN_POLLING` references: 12.

Interpretation:
- Daily cleanup model is now verified against MBBSEmu host behavior, but MajorMUD item cleanup/delete/hide semantics remain unpromoted until module routines are mapped.
- MajorMUD has named background scheduler routines; monster creation should be analyzed through `_BACKGROUND_MONSTER_CREATE` next.


## Validation0003 — MDB Extractor Tool Intake

Only `tools/offline_mdb_text_extractor.py` from the uploaded EditorPatch0003 MDB Extractor Correction package was admitted into the validation tree. Extracted rows, patch reports, and the editor patch manifest were intentionally excluded. This tool may be used in later validation passes to cross-check MDB extraction behavior, but it is not itself game runtime evidence and does not justify `/Data` promotion.

## Validation0003 — External Evidence Promotion Notes

Status: completed.

Validated against MBBSEmu/MajorMUD disassembly evidence, not yet promoted into `/Data`:

- MajorMUD registers multiple background routines through host scheduler mechanisms (`RTKICK` / `_MY_RTKICK`).
- Cleanup is a real MajorMUD routine/concept, with disassembly evidence for `Starting Cleanup` and `Entering cleanup`.
- Cleanup-related item attributes exist: `Delete at cleanup`, `Recharge at cleanup`, `Delete from inventory at cleanup`, and `Remain Visible at cleanup`.
- Search is an implemented command and has a hidden-exit routine path (`_SEARCH_FOR_HIDDEN_EXITS`).
- Room/area reset logic has direct monster-generation messaging: monsters start generating appropriately after reset.

Still not promoted into `/Data`:

- Exact data flag offsets and field mappings for cleanup behavior.
- Exact Search formula and all searchable target types.
- Exact normal monster global interval.
- Exact per-monster death timer implementation.
- Exact boss/unique/quest persistence behavior.



## Validation0004 — Combat, Quest, Persistence External Evidence

- MBBSEmu host evidence supports module lifecycle, polling, RTKICK, Btrieve/storage, user/channel, event, and timer concepts.
- MajorMUD disassembly contains candidate neighborhoods for combat, attack, damage, quest, save/load, monster, room, gold, experience, and spell logic.
- No exact combat formula, quest persistence rule, or save-layout rule is promoted yet. Function-level mapping remains required.


## Validation0005 — Promotion Readiness Update

- Host/module polling and RTKICK-style scheduling are now validated for documentation-level modeling through MBBSEmu host evidence and MajorMUD disassembly references.
- Search and hidden discovery path are validated for documentation-level modeling through disassembly routine clusters. Exact formula remains unpromoted.
- No `/Data` promotion is approved. Exact field binding remains required for cleanup flags, respawn timers, quest persistence, boss uniqueness, and combat formulas.


---

# Validation0006 Engine Knowledge Promotions

## Promoted to engine documentation
- Host nightly cleanup lifecycle exists: MBBSEmu schedules daily cleanup, warns users, removes sessions, calls module `mcurou`, then calls module `finrou`.
- RTKICK is a delayed one-shot scheduling primitive measured in seconds and removed after execution.
- BEGIN_POLLING is a persistent polling primitive that runs until STOP_POLLING.
- MajorMUD has a search/hidden-discovery subsystem: `_CMD_SEARCH`, `_CLEAR_SEARCH_FLAGS`, hidden/hide references.

## Not promoted to `/Data`
- Item cleanup flags remain unmapped.
- Exact search formula remains unmapped.
- Monster respawn exact scheduling remains unmapped.
- Quest persistence fields and transitions remain unmapped.

## Evidence classification
These are documentation-level engine validations from MBBSEmu and the MajorMUD disassembly. They are not direct `/Data` changes.


## Validation0007 Update

Validation0007 performed a focused routine/term review of remaining promotion blockers. It found stronger targeting evidence for cleanup, search/hidden discovery, respawn/reset/timer, and quest persistence routines, but did not isolate exact data fields, bit flags, or formulas.

Promotion state remains unchanged from Validation0006: host cleanup lifecycle, RTKICK one-shot scheduling, BEGIN_POLLING persistent polling, and search/hidden-discovery subsystem are validated at engine-documentation level only. Exact cleanup flags, exact monster respawn scheduler behavior, exact search formula, and quest persistence fields remain unresolved.


## Validation0008 — Search and Hidden-Exit Routine Promotion

- Directional hidden-exit searching is now routine-level validated. `_CMD_SEARCH` parses `SEARCH <direction>` and calls `_SEARCH_FOR_HIDDEN_EXITS`.
- Hidden-exit discovery uses a random roll, room exit state/flags, and player-derived thresholds.
- Successful discovery mutates room/exit visibility state and calls `_ADD_EXIT_TO_LIST`.
- Exact named stat/skill mapping for player offsets remains unresolved; `/Data` is unchanged.


## Validation0009 — Monster Respawn Scheduler Targeted Rerun

Status: completed after rerun; earlier quick Validation0009/Validation0010 summaries are discarded.

Validated for engine documentation:

- MajorMUD has a dedicated background monster creation routine (`_BACKGROUND_MONSTER_CREATE`) that calls the monster generation driver and re-registers itself through `_MY_RTKICK`.
- MBBSEmu confirms RTKICK semantics as one-shot delayed callbacks; repeated background behavior must be re-registered by the module.
- MajorMUD separates monster updates into slow, medium, and fast pipelines.
- Monster generation checks room monster slots before placement, so slot occupancy can block generation.
- Explicit elapsed-time gating exists inside `_GENERATE_MONSTER`, using stored date/time fields, `_CALC_MINUTES_DIFFERENCE`, and random jitter. Exact field names and `/Data` mappings remain unresolved.

Not promoted:

- Exact 15-minute normal monster respawn interval.
- Exact per-monster timer field mapping.
- Exact cleanup interaction with timer state.
- Quest/boss-specific respawn flags.

## Validation0010 Promotions — Field-Level Reverse Mapping

### Promoted to engine documentation
- `_CMD_SEARCH` and `_SEARCH_FOR_HIDDEN_EXITS` now have offset-level control-flow validation. Hidden-exit search uses a dedicated directional command path, `GENRDN(0,100)`, a player offset threshold at `+5EDh` adjusted by `-15` with a minimum of `3`, room exit type `6`, and hidden flag bit `0x02` at the room exit flag field. Successful discovery mutates room exit state and calls `_ADD_EXIT_TO_LIST`.
- `_GENERATE_MONSTER` now has offset-level validation for room occupancy gates, 15-slot room monster occupancy scanning, time-gate logic, active-count gates, and unique active monster allocation. This validates generation/respawn gate architecture but not the exact named fields in `/Data`.

### Not promoted to /Data
- No `/Data` change was made. Offsets are validated, but canonical field names/columns remain unresolved.
- Cleanup destroy/hide flags, exact quest persistence fields, and exact named monster timer fields remain unresolved.

## Validation0011 — Cleanup and Quest Field-Level Blockers

Validation0011 refined the remaining cleanup and quest persistence questions into field-level mapping blockers.

Promoted to engine documentation:

- Cleanup lifecycle remains verified at architecture level, but exact item flag encoding is unresolved.
- Quest persistence remains verified at architecture level, but exact Btrieve/player-state fields are unresolved.
- Future promotion requires field-offset or record-column evidence, not keyword or subsystem-level evidence.

No `/Data` changes were made.

## Validation0012 — Field-Level Reverse Mapping Targets

Validation0012 produced a conservative candidate target set for cleanup flag encoding and quest persistence field mapping.

Promoted to engine documentation:

- Cleanup and quest validation have moved from architecture-level questions to field/offset reverse-mapping targets.
- Exact `/Data` field promotions remain blocked until a source field or offset can be tied directly to runtime behavior.

No `/Data` changes were made.

## Validation0013 — Quest Gate Single-Path Trace

Validation0013 promotes one engine-documentation finding:

- A load-time player status/quest gate exists and can emit the message requiring the player to complete a quest to regain Saint status.

The pass also traced `_ASK_MONSTER_A_QUESTION` as a concrete interactive monster/question text path.

No `/Data` changes were made. Exact quest persistence fields and offset names remain unresolved.
